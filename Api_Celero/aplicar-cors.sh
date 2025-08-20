#!/bin/bash

# Script para aplicar cambios de configuración CORS y Nginx
# Uso: sudo ./aplicar-cors.sh

set -e

# Colores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Funciones de utilidad
log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Verificar si se ejecuta como root
if [ "$EUID" -ne 0 ]; then
    log_error "Este script debe ejecutarse como root (usar sudo)"
    exit 1
fi

# Configuración
APP_NAME="api-celero"
APP_DIR="/var/www/$APP_NAME"
NGINX_CONF="/etc/nginx/sites-available/$APP_NAME"
SERVICE_NAME="$APP_NAME.service"

# 1. Copiar archivos de configuración actualizados
log_info "Copiando archivos de configuración actualizados..."

# Copiar nginx.conf actualizado
if [ -f "./nginx.conf" ]; then
    cp ./nginx.conf $NGINX_CONF
    log_info "Configuración de Nginx actualizada"
    
    # Verificar si existe el enlace simbólico, si no, crearlo
    if [ ! -L "/etc/nginx/sites-enabled/$APP_NAME" ]; then
        ln -s $NGINX_CONF /etc/nginx/sites-enabled/
        log_info "Enlace simbólico creado para Nginx"
    fi
else
    log_warn "Archivo nginx.conf no encontrado"
fi

# Copiar servicio systemd actualizado
if [ -f "./api-celero.service" ]; then
    cp ./api-celero.service /etc/systemd/system/$SERVICE_NAME
    systemctl daemon-reload
    log_info "Servicio systemd actualizado"
else
    log_warn "Archivo api-celero.service no encontrado"
fi

# Copiar configuración actualizada
if [ -f "./appsettings.Production.json" ]; then
    cp ./appsettings.Production.json $APP_DIR/
    log_info "Configuración de aplicación actualizada"
else
    log_warn "Archivo appsettings.Production.json no encontrado"
fi

# 2. Reiniciar servicios
log_info "Reiniciando servicios..."
systemctl restart $SERVICE_NAME
systemctl restart nginx

# 3. Verificar estado
log_info "Verificando estado de los servicios..."
if systemctl is-active --quiet $SERVICE_NAME && systemctl is-active --quiet nginx; then
    log_info "✅ Configuración aplicada exitosamente!"
    log_info "API funcionando en: http://localhost:8080"
    log_info "Nginx funcionando correctamente"
    
    # Verificar CORS con curl
    log_info "Probando encabezados CORS..."
    CORS_TEST=$(curl -s -I -X OPTIONS -H "Origin: https://selfservice-dev.celero.network" -H "Access-Control-Request-Method: GET" http://localhost:8080/api/clientes/health | grep -i "Access-Control-Allow")
    
    if [ -n "$CORS_TEST" ]; then
        log_info "Encabezados CORS configurados correctamente:"
        echo "$CORS_TEST"
    else
        log_warn "No se detectaron encabezados CORS en la respuesta. Verifica la configuración de la aplicación."
    fi
else
    if ! systemctl is-active --quiet $SERVICE_NAME; then
        log_error "❌ El servicio $SERVICE_NAME no está activo"
        log_error "Revisar logs: journalctl -u $SERVICE_NAME --no-pager -n 50"
    fi
    
    if ! systemctl is-active --quiet nginx; then
        log_error "❌ Nginx no está activo"
        log_error "Revisar logs: journalctl -u nginx --no-pager -n 50"
    fi
    
    exit 1
fi

log_info "Para verificar completamente que CORS funciona, prueba desde el frontend:"
log_info "1. Abre la consola del navegador en https://selfservice-dev.celero.network"
log_info "2. Ejecuta: fetch('https://api.celero.network/api/clientes/health').then(r => console.log(r.ok))"
log_info "Deberías ver 'true' como resultado si CORS está funcionando correctamente"
