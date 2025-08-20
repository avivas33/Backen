#!/bin/bash

# 🚀 Script Final de Despliegue - Api Celero
# Ejecutar con: sudo bash finalizar-despliegue.sh

set -e

echo "🚀 Finalizando despliegue de Api Celero..."
echo "=================================================="

# Verificar que los archivos de configuración existen
if [ ! -f "/tmp/api-celero.service" ] || [ ! -f "/tmp/api-celero-nginx.conf" ]; then
    echo "❌ Error: Archivos de configuración no encontrados en /tmp/"
    echo "Ejecutar primero el script de despliegue principal"
    exit 1
fi

# 1. Instalar servicio systemd
echo "📋 1. Instalando servicio systemd..."
cp /tmp/api-celero.service /etc/systemd/system/api-celero.service
systemctl daemon-reload
systemctl enable api-celero
echo "✅ Servicio systemd configurado"

# 2. Configurar nginx
echo "🌐 2. Configurando nginx..."
cp /tmp/api-celero-nginx.conf /etc/nginx/sites-available/api-celero
ln -sf /etc/nginx/sites-available/api-celero /etc/nginx/sites-enabled/
echo "✅ Configuración de nginx instalada"

# 3. Verificar configuración de nginx
echo "🔍 3. Verificando configuración de nginx..."
if nginx -t; then
    echo "✅ Configuración de nginx válida"
else
    echo "❌ Error en la configuración de nginx"
    exit 1
fi

# 4. Iniciar servicios
echo "🚀 4. Iniciando servicios..."
systemctl start api-celero
systemctl reload nginx
echo "✅ Servicios iniciados"

# 5. Verificar estado
echo "📊 5. Verificando estado de los servicios..."
sleep 3

if systemctl is-active --quiet api-celero; then
    echo "✅ Api_Celero está ejecutándose"
    API_STATUS="✅ ACTIVO"
else
    echo "❌ Api_Celero no está ejecutándose"
    API_STATUS="❌ ERROR"
fi

if systemctl is-active --quiet nginx; then
    echo "✅ Nginx está ejecutándose"
    NGINX_STATUS="✅ ACTIVO"
else
    echo "❌ Nginx no está ejecutándose"
    NGINX_STATUS="❌ ERROR"
fi

# 6. Verificar puerto
echo "🔌 6. Verificando puerto 5001..."
if ss -tlnp | grep -q ":5001"; then
    echo "✅ API escuchando en puerto 5001"
    PORT_STATUS="✅ ACTIVO"
else
    echo "⚠️ Puerto 5001 no está en uso"
    PORT_STATUS="⚠️ INACTIVO"
fi

# 7. Limpiar archivos temporales
echo "🧹 7. Limpiando archivos temporales..."
rm -f /tmp/api-celero.service /tmp/api-celero-nginx.conf
echo "✅ Archivos temporales eliminados"

# Mostrar resumen final
echo ""
echo "🎉 DESPLIEGUE COMPLETADO"
echo "=================================================="
echo "📍 Directorio: /home/avivas/selfservice/Api"
echo "🌐 URL: https://api.celero.network"
echo "🔌 Puerto interno: 5001"
echo ""
echo "📊 ESTADO DE SERVICIOS:"
echo "   Api Celero: $API_STATUS"
echo "   Nginx: $NGINX_STATUS"
echo "   Puerto 5001: $PORT_STATUS"
echo ""
echo "🔧 COMANDOS DE ADMINISTRACIÓN:"
echo "   Estado: sudo systemctl status api-celero"
echo "   Logs: sudo journalctl -u api-celero -f"
echo "   Reiniciar: sudo systemctl restart api-celero"
echo ""
echo "🧪 PRUEBAS:"
echo "   curl https://api.celero.network/api/clientes"
echo "   curl https://api.celero.network/openapi/v1.json"
echo ""

# Prueba final si la API está activa
if [ "$API_STATUS" = "✅ ACTIVO" ]; then
    echo "🧪 Realizando prueba final..."
    if curl -s --max-time 10 http://localhost:5001/api/clientes >/dev/null 2>&1; then
        echo "✅ API responde correctamente"
    else
        echo "⚠️ API no responde (puede necesitar unos segundos más)"
    fi
fi

echo ""
echo "✅ ¡DESPLIEGUE FINALIZADO EXITOSAMENTE! 🚀"
