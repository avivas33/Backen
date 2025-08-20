#!/bin/bash

# Script de despliegue para Api_Celero en Ubuntu
# Uso: ./deploy.sh

set -e

echo "🚀 Iniciando despliegue de Api_Celero..."

# Configuración
APP_NAME="api-celero"
APP_DIR="/var/www/$APP_NAME"
SERVICE_NAME="$APP_NAME.service"
USER="www-data"
GROUP="www-data"

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

# 1. Detener el servicio si existe
log_info "Deteniendo servicio si existe..."
if systemctl is-active --quiet $SERVICE_NAME; then
    systemctl stop $SERVICE_NAME
    log_info "Servicio $SERVICE_NAME detenido"
fi

# 2. Crear directorio de aplicación
log_info "Creando directorios..."
mkdir -p $APP_DIR
mkdir -p /var/log/$APP_NAME

# 3. Copiar archivos de la aplicación
log_info "Copiando archivos de aplicación..."
if [ -d "./publish" ]; then
    cp -r ./publish/* $APP_DIR/
else
    log_error "Directorio ./publish no encontrado. Ejecute primero: dotnet publish -c Release -r linux-x64 --self-contained false"
    exit 1
fi

# 3.1. Copiar base de datos SQLite si existe
if [ -f "./recibos_offline.db" ]; then
    log_info "Copiando base de datos SQLite..."
    cp ./recibos_offline.db $APP_DIR/
fi

# 4. Establecer permisos
log_info "Configurando permisos..."
chown -R $USER:$GROUP $APP_DIR
chown -R $USER:$GROUP /var/log/$APP_NAME
chmod +x $APP_DIR/Api_Celero

# 5. Crear archivo de servicio systemd
log_info "Creando servicio systemd..."
cat > /etc/systemd/system/$SERVICE_NAME << EOF
[Unit]
Description=Api Celero ASP.NET Web API
After=network.target

[Service]
Type=simple
# Configuración del servicio
WorkingDirectory=$APP_DIR
ExecStart=/usr/bin/dotnet $APP_DIR/Api_Celero.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=$APP_NAME
User=$USER
Group=$GROUP

# Variables de entorno
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:8080
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Variables de aplicación (configurar según sea necesario)
# Environment=GOOGLE_RECAPTCHA_PROJECT_ID=your-project-id
# Environment=GOOGLE_RECAPTCHA_API_KEY=your-api-key
# Environment=GOOGLE_RECAPTCHA_SITE_KEY=your-site-key
# Environment=RESEND_API_KEY=your-resend-key
# Environment=RESEND_FROM_EMAIL=your-email
# Environment=RESEND_FROM_NAME=your-name

[Install]
WantedBy=multi-user.target
EOF

# 6. Recargar systemd y habilitar servicio
log_info "Configurando systemd..."
systemctl daemon-reload
systemctl enable $SERVICE_NAME

# 7. Iniciar servicio
log_info "Iniciando servicio..."
systemctl start $SERVICE_NAME

# 8. Verificar estado
sleep 5
if systemctl is-active --quiet $SERVICE_NAME; then
    log_info "✅ Despliegue completado exitosamente!"
    log_info "Estado del servicio:"
    systemctl status $SERVICE_NAME --no-pager
    log_info ""
    log_info "La API está ejecutándose en: http://localhost:8080"
    log_info "Para ver logs: journalctl -u $SERVICE_NAME -f"
else
    log_error "❌ Error al iniciar el servicio"
    log_error "Revisar logs: journalctl -u $SERVICE_NAME --no-pager"
    exit 1
fi
