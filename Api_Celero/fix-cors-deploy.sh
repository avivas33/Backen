#!/bin/bash

echo "==================================="
echo "Fix CORS para API Celero"
echo "==================================="

# Colores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Función para imprimir con color
print_status() {
    echo -e "${GREEN}[✓]${NC} $1"
}

print_error() {
    echo -e "${RED}[✗]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[!]${NC} $1"
}

# 1. Verificar que estamos en el directorio correcto
if [ ! -f "Api_Celero.csproj" ]; then
    print_error "No se encuentra Api_Celero.csproj. Asegúrate de ejecutar este script desde el directorio Api_Celero/"
    exit 1
fi

print_status "Directorio correcto verificado"

# 2. Hacer backup de archivos importantes
print_status "Creando backups..."
cp Program.cs Program.cs.backup.$(date +%Y%m%d_%H%M%S) 2>/dev/null
cp nginx.conf nginx.conf.backup.$(date +%Y%m%d_%H%M%S) 2>/dev/null
cp appsettings.Production.json appsettings.Production.json.backup.$(date +%Y%m%d_%H%M%S) 2>/dev/null

# 3. Aplicar el Program.cs corregido
if [ -f "Program-CORS-Fixed.cs" ]; then
    print_status "Aplicando Program.cs con CORS corregido..."
    cp Program-CORS-Fixed.cs Program.cs
else
    print_warning "Program-CORS-Fixed.cs no encontrado, manteniendo Program.cs actual"
fi

# 4. Compilar y publicar la aplicación
print_status "Compilando la aplicación..."
dotnet build -c Release

if [ $? -ne 0 ]; then
    print_error "Error en la compilación. Revisa los errores anteriores."
    exit 1
fi

print_status "Publicando la aplicación..."
dotnet publish -c Release -o ./publish

if [ $? -ne 0 ]; then
    print_error "Error en la publicación. Revisa los errores anteriores."
    exit 1
fi

# 5. Si estamos en el servidor de producción, actualizar nginx
if [ -f "/etc/nginx/sites-available/api-celero" ]; then
    print_status "Detectado servidor de producción. Actualizando configuración de nginx..."
    
    # Backup de la configuración actual de nginx
    sudo cp /etc/nginx/sites-available/api-celero /etc/nginx/sites-available/api-celero.backup.$(date +%Y%m%d_%H%M%S)
    
    # Aplicar la nueva configuración
    if [ -f "nginx-cors-fixed.conf" ]; then
        sudo cp nginx-cors-fixed.conf /etc/nginx/sites-available/api-celero
        print_status "Nueva configuración de nginx aplicada"
    else
        sudo cp nginx.conf /etc/nginx/sites-available/api-celero
        print_warning "Usando nginx.conf estándar"
    fi
    
    # Verificar la configuración de nginx
    sudo nginx -t
    if [ $? -eq 0 ]; then
        print_status "Configuración de nginx válida"
        
        # Recargar nginx
        sudo systemctl reload nginx
        print_status "Nginx recargado"
    else
        print_error "Error en la configuración de nginx. Restaurando backup..."
        sudo cp /etc/nginx/sites-available/api-celero.backup.$(date +%Y%m%d_%H%M%S) /etc/nginx/sites-available/api-celero
        sudo systemctl reload nginx
        exit 1
    fi
    
    # Reiniciar el servicio de la API
    if systemctl is-active --quiet api-celero; then
        print_status "Reiniciando servicio api-celero..."
        sudo systemctl restart api-celero
        sleep 3
        
        # Verificar que el servicio esté corriendo
        if systemctl is-active --quiet api-celero; then
            print_status "Servicio api-celero reiniciado correctamente"
        else
            print_error "El servicio api-celero no se inició correctamente"
            sudo journalctl -u api-celero -n 50
        fi
    else
        print_warning "Servicio api-celero no está activo. Iniciándolo..."
        sudo systemctl start api-celero
    fi
else
    print_warning "No se detectó servidor de producción. Para aplicar en producción:"
    echo ""
    echo "1. Copia ./publish/* al servidor"
    echo "2. Copia nginx-cors-fixed.conf al servidor como /etc/nginx/sites-available/api-celero"
    echo "3. Ejecuta: sudo nginx -t && sudo systemctl reload nginx"
    echo "4. Reinicia el servicio: sudo systemctl restart api-celero"
fi

# 6. Instrucciones para prueba
echo ""
echo "==================================="
echo "${GREEN}Despliegue completado${NC}"
echo "==================================="
echo ""
echo "Para probar CORS:"
echo "1. Abre test-cors.html en un navegador"
echo "2. O ejecuta: curl -I -X OPTIONS https://api.celero.network/api/clientes \\"
echo "   -H 'Origin: https://selfservice-dev.celero.network' \\"
echo "   -H 'Access-Control-Request-Method: GET' \\"
echo "   -H 'Access-Control-Request-Headers: Content-Type'"
echo ""
echo "Los headers CORS deberían incluir:"
echo "- Access-Control-Allow-Origin: https://selfservice-dev.celero.network"
echo "- Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS, PATCH"
echo "- Access-Control-Allow-Headers: [lista de headers]"
echo "- Access-Control-Allow-Credentials: true"
echo ""

# 7. Test rápido si curl está disponible
if command -v curl &> /dev/null; then
    print_status "Ejecutando prueba rápida de CORS..."
    echo ""
    curl -I -X OPTIONS https://api.celero.network/api/clientes \
        -H 'Origin: https://selfservice-dev.celero.network' \
        -H 'Access-Control-Request-Method: GET' \
        -H 'Access-Control-Request-Headers: Content-Type' 2>/dev/null | grep -i "access-control"
fi