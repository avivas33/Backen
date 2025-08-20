#!/bin/bash

# Script para verificar headers CORS de la API
# Uso: ./verificar-cors.sh [URL_BASE]

# Colores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# URL base por defecto
URL_BASE=${1:-"http://localhost:8080"}

# Lista de endpoints a probar
ENDPOINTS=(
    "/api/clientes/health"
    "/api/clientes/verificar-config"
)

# Lista de orígenes a probar
ORIGINS=(
    "https://selfservice-dev.celero.network"
    "https://selfservice.celero.network"
    "https://celero.network"
)

echo -e "${BLUE}=== Verificando configuración CORS para ${URL_BASE} ===${NC}"

# Función para probar un endpoint con un origen específico
test_cors() {
    local url=$1
    local origin=$2
    
    echo -e "\n${YELLOW}Probando $url con Origin: $origin${NC}"
    
    # Prueba OPTIONS (preflight)
    echo -e "\n${BLUE}Enviando solicitud OPTIONS (preflight):${NC}"
    OPTIONS_RESULT=$(curl -s -I -X OPTIONS \
        -H "Origin: $origin" \
        -H "Access-Control-Request-Method: GET" \
        -H "Access-Control-Request-Headers: Content-Type" \
        "$url")
    
    # Verificar encabezados CORS en la respuesta OPTIONS
    ALLOW_ORIGIN=$(echo "$OPTIONS_RESULT" | grep -i "Access-Control-Allow-Origin")
    ALLOW_METHODS=$(echo "$OPTIONS_RESULT" | grep -i "Access-Control-Allow-Methods")
    ALLOW_HEADERS=$(echo "$OPTIONS_RESULT" | grep -i "Access-Control-Allow-Headers")
    ALLOW_CREDENTIALS=$(echo "$OPTIONS_RESULT" | grep -i "Access-Control-Allow-Credentials")
    
    # Imprimir resultados OPTIONS
    if [[ -n "$ALLOW_ORIGIN" && -n "$ALLOW_METHODS" ]]; then
        echo -e "${GREEN}✓ Respuesta OPTIONS correcta${NC}"
        echo "$OPTIONS_RESULT" | grep -i "Access-Control"
    else
        echo -e "${RED}✗ Faltan encabezados CORS en la respuesta OPTIONS${NC}"
        echo "$OPTIONS_RESULT"
    fi
    
    # Prueba GET
    echo -e "\n${BLUE}Enviando solicitud GET:${NC}"
    GET_RESULT=$(curl -s -I -X GET \
        -H "Origin: $origin" \
        -H "Accept: application/json" \
        "$url")
    
    # Verificar encabezados CORS en la respuesta GET
    ALLOW_ORIGIN_GET=$(echo "$GET_RESULT" | grep -i "Access-Control-Allow-Origin")
    
    # Imprimir resultados GET
    if [[ -n "$ALLOW_ORIGIN_GET" ]]; then
        echo -e "${GREEN}✓ Respuesta GET correcta${NC}"
        echo "$GET_RESULT" | grep -i "Access-Control"
    else
        echo -e "${RED}✗ Faltan encabezados CORS en la respuesta GET${NC}"
        echo "$GET_RESULT"
    fi
}

# Probar cada endpoint con cada origen
for endpoint in "${ENDPOINTS[@]}"; do
    for origin in "${ORIGINS[@]}"; do
        test_cors "${URL_BASE}${endpoint}" "$origin"
        echo -e "\n${BLUE}----------------------------------------${NC}"
    done
done

echo -e "\n${GREEN}Pruebas CORS completadas.${NC}"
echo -e "Si ves problemas, verifica:"
echo -e "1. Configuración CORS en Program.cs"
echo -e "2. Configuración AllowedOrigins en appsettings.json/appsettings.Production.json"
echo -e "3. Configuración del proxy_pass en Nginx para pasar correctamente los headers"
