#!/bin/bash

# Script para arreglar los problemas de CORS en la API
# Uso: sudo ./fix-cors.sh

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
SERVICE_NAME="$APP_NAME.service"
NGINX_CONF="/etc/nginx/sites-available/$APP_NAME"
NGINX_LINK="/etc/nginx/sites-enabled/$APP_NAME"

# 1. Crear archivo .env con configuración adicional
log_info "Creando archivo .env con configuración adicional..."
cat > $APP_DIR/.env << EOF
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:8080
ALLOWED_ORIGINS=https://selfservice-dev.celero.network,https://selfservice.celero.network,https://celero.network,https://api.celero.network
EOF

# Ajustar permisos del archivo .env
chown www-data:www-data $APP_DIR/.env
chmod 600 $APP_DIR/.env

# 2. Crear configuración Nginx con soporte SSL
log_info "Configurando Nginx con soporte SSL..."
cat > $NGINX_CONF << EOF
server {
    listen 80;
    server_name api.celero.network;
    
    # Redirect HTTP to HTTPS
    return 301 https://\$host\$request_uri;
}

server {
    listen 443 ssl http2;
    server_name api.celero.network;
    
    # Configuración SSL
    ssl_certificate /etc/letsencrypt/live/celero.network/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/celero.network/privkey.pem;

    # Configuraciones SSL adicionales
    ssl_session_timeout 1d;
    ssl_session_cache shared:MozTLS:10m;
    ssl_session_tickets off;

    # Protocolos y cifrados modernos
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384;
    ssl_prefer_server_ciphers off;

    # Headers de seguridad
    add_header Strict-Transport-Security "max-age=63072000" always;
    add_header X-Content-Type-Options nosniff;
    add_header X-Frame-Options DENY;
    add_header X-XSS-Protection "1; mode=block";
    
    location / {
        # Manejo especial para solicitudes OPTIONS (preflight)
        if (\$request_method = 'OPTIONS') {
            add_header 'Access-Control-Allow-Origin' 'https://selfservice-dev.celero.network' always;
            add_header 'Access-Control-Allow-Methods' 'GET, POST, PUT, DELETE, OPTIONS' always;
            add_header 'Access-Control-Allow-Headers' 'DNT,User-Agent,X-Requested-With,If-Modified-Since,Cache-Control,Content-Type,Range,Authorization' always;
            add_header 'Access-Control-Allow-Credentials' 'true' always;
            add_header 'Access-Control-Max-Age' 1728000;
            add_header 'Content-Type' 'text/plain charset=UTF-8';
            add_header 'Content-Length' 0;
            return 204;
        }

        # Agregar encabezados CORS para todas las demás solicitudes
        add_header 'Access-Control-Allow-Origin' 'https://selfservice-dev.celero.network' always;
        add_header 'Access-Control-Allow-Methods' 'GET, POST, PUT, DELETE, OPTIONS' always;
        add_header 'Access-Control-Allow-Headers' 'DNT,User-Agent,X-Requested-With,If-Modified-Since,Cache-Control,Content-Type,Range,Authorization' always;
        add_header 'Access-Control-Allow-Credentials' 'true' always;

        proxy_pass http://localhost:8080;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
        
        # Timeouts más largos para peticiones lentas
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
        
        # Buffer settings
        proxy_buffering on;
        proxy_buffer_size 128k;
        proxy_buffers 4 256k;
        proxy_busy_buffers_size 256k;
    }
    
    # Logs
    access_log /var/log/nginx/api-celero.access.log;
    error_log /var/log/nginx/api-celero.error.log;
}
EOF

# Crear enlace simbólico si no existe
if [ ! -L "$NGINX_LINK" ]; then
    ln -s $NGINX_CONF $NGINX_LINK
fi

# 3. Actualizar configuración del servicio systemd
log_info "Actualizando configuración del servicio systemd..."
cat > /etc/systemd/system/$SERVICE_NAME << EOF
[Unit]
Description=Api Celero ASP.NET Web API
After=network.target

[Service]
Type=simple
WorkingDirectory=$APP_DIR
ExecStart=/usr/bin/dotnet $APP_DIR/Api_Celero.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=$APP_NAME
User=www-data
Group=www-data

# Variables de entorno
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:8080
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Cargar variables adicionales desde archivo
EnvironmentFile=$APP_DIR/.env

[Install]
WantedBy=multi-user.target
EOF

# 4. Comprobar que la base de datos existe y tiene los permisos correctos
log_info "Verificando la base de datos SQLite..."
if [ ! -f "$APP_DIR/recibos_offline.db" ]; then
    log_warn "La base de datos no existe en $APP_DIR, copiando desde el directorio actual..."
    if [ -f "./recibos_offline.db" ]; then
        cp ./recibos_offline.db $APP_DIR/
    else
        log_error "No se encontró la base de datos SQLite. Cree manualmente el archivo o cópielo desde otra ubicación."
    fi
fi

# Asegurar permisos correctos para la base de datos
chown www-data:www-data $APP_DIR/recibos_offline.db
chmod 664 $APP_DIR/recibos_offline.db

# 5. Reiniciar servicios
log_info "Recargando configuración y reiniciando servicios..."
systemctl daemon-reload
systemctl restart $SERVICE_NAME
systemctl restart nginx

# 6. Verificar el estado de los servicios
log_info "Verificando el estado de los servicios..."
sleep 3

if systemctl is-active --quiet $SERVICE_NAME && systemctl is-active --quiet nginx; then
    log_info "✅ Los servicios están funcionando correctamente"
    
    # Prueba final para verificar CORS
    log_info "Realizando prueba final de CORS..."
    RESULT=$(curl -s -I -X OPTIONS \
      -H "Origin: https://selfservice-dev.celero.network" \
      -H "Access-Control-Request-Method: GET" \
      https://api.celero.network/api/clientes/health)
    
    if echo "$RESULT" | grep -q "Access-Control-Allow-Origin"; then
        log_info "✅ CORS configurado correctamente. Resultado de la prueba:"
        echo "$RESULT" | grep -i "Access-Control"
    else
        log_warn "⚠️ No se detectaron encabezados CORS en la respuesta. Es posible que aún haya problemas."
        echo "$RESULT"
    fi
    
    log_info "Prueba completa. Si sigue habiendo problemas de CORS, verifica los siguientes aspectos:"
    log_info "1. Asegúrate de que los certificados SSL estén correctamente instalados y sean válidos"
    log_info "2. Verifica que no haya otros archivos de configuración de Nginx que interfieran"
    log_info "3. Comprueba que el firewall no esté bloqueando las peticiones"
    log_info "4. Revisa los logs de Nginx: tail -f /var/log/nginx/api-celero.error.log"
    log_info "5. Revisa los logs del servicio: journalctl -u api-celero -f"
else
    log_error "❌ Hay problemas con los servicios"
    
    if ! systemctl is-active --quiet $SERVICE_NAME; then
        log_error "El servicio $SERVICE_NAME no está activo"
        journalctl -u $SERVICE_NAME --no-pager -n 30
    fi
    
    if ! systemctl is-active --quiet nginx; then
        log_error "Nginx no está activo"
        journalctl -u nginx --no-pager -n 30
        nginx -t
    fi
fi
