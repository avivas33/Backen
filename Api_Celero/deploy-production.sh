#!/bin/bash

# 🚀 Script de Despliegue para Producción - Api_Celero
# Carpeta de destino: /home/avivas/selfservice/Api
# Certificados SSL ya configurados en celero.network

set -e  # Salir si cualquier comando falla

# Colores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

print_step() {
    echo -e "${BLUE}=== $1 ===${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

# Configuración
APP_DIR="/home/avivas/selfservice/Api"
TEMP_DIR="/tmp/api-celero-deploy"
SERVICE_NAME="api-celero"
APP_PORT="5001"

print_step "Iniciando despliegue de Api_Celero en producción"
echo "📍 Directorio de destino: $APP_DIR"
echo "🌐 Puerto de la aplicación: $APP_PORT"
echo "🔒 SSL: Ya configurado en celero.network"
echo ""

# Verificar que estamos en el directorio correcto
if [ ! -f "Api_Celero.csproj" ]; then
    print_error "No se encontró Api_Celero.csproj. Ejecuta este script desde el directorio del proyecto."
    exit 1
fi

# Detener servicio si existe
print_step "Verificando servicios existentes"
if systemctl is-active --quiet $SERVICE_NAME; then
    print_warning "Deteniendo servicio existente..."
    sudo systemctl stop $SERVICE_NAME
    print_success "Servicio detenido"
fi

# Crear backup si existe instalación anterior
if [ -d "$APP_DIR" ]; then
    print_step "Creando backup de la instalación anterior"
    BACKUP_DIR="/home/avivas/selfservice/backups/api-$(date +%Y%m%d_%H%M%S)"
    sudo mkdir -p "$(dirname "$BACKUP_DIR")"
    sudo cp -r "$APP_DIR" "$BACKUP_DIR"
    print_success "Backup creado en: $BACKUP_DIR"
fi

# Crear directorio temporal y de la aplicación
print_step "Preparando directorios"
rm -rf $TEMP_DIR
mkdir -p $TEMP_DIR
sudo mkdir -p $APP_DIR
sudo mkdir -p /home/avivas/selfservice/logs
print_success "Directorios preparados"

# Publicar la aplicación
print_step "Compilando y publicando aplicación"
dotnet publish -c Release -r linux-x64 --self-contained true -o $TEMP_DIR
print_success "Aplicación publicada exitosamente"

# Copiar archivos a directorio final
print_step "Desplegando archivos"
sudo rm -rf $APP_DIR/*
sudo cp -r $TEMP_DIR/* $APP_DIR/
rm -rf $TEMP_DIR
print_success "Archivos desplegados"

# Crear archivo de configuración de producción
print_step "Configurando variables de entorno"
sudo tee $APP_DIR/appsettings.Production.json > /dev/null <<EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    },
    "Console": {
      "IncludeScopes": true
    }
  },
  "AllowedHosts": "*",
  "AllowedOrigins": [
    "https://selfservice.celero.network",
    "https://selfservice-dev.celero.network",
    "https://celero.network",
    "https://api.celero.network"
  ],
  "GoogleRecaptcha": {
    "ProjectId": "celero-apps",
    "ApiKey": "AIzaSyCj_pLq52sr846YrxrUJ_9StrFHHxQ1unY",
    "SiteKey": "6Lfj1mArAAAAAIkj3BJGhSMpIdUT6qnCa1aUMrRN"
  },
  "Resend": {
    "ApiKey": "re_af22v2eR_5UeSvpks4dfnJWTe6dLYJs5a",
    "DefaultFromEmail": "noreply@celero.network",
    "DefaultFromName": "Celero Network"
  }
}
EOF

# Crear archivo de variables de entorno
sudo tee $APP_DIR/.env > /dev/null <<EOF
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:$APP_PORT
PORT=$APP_PORT
ALLOWED_ORIGINS=https://selfservice.celero.network,https://selfservice-dev.celero.network,https://celero.network,https://api.celero.network
EOF

print_success "Configuración de producción creada"

# Configurar permisos
print_step "Configurando permisos"
sudo chown -R $USER:$USER $APP_DIR
sudo chmod +x $APP_DIR/Api_Celero
print_success "Permisos configurados"

# Crear servicio systemd
print_step "Configurando servicio systemd"
sudo tee /etc/systemd/system/$SERVICE_NAME.service > /dev/null <<EOF
[Unit]
Description=Api Celero - ASP.NET 9 Web API
After=network.target

[Service]
Type=notify
User=$USER
Group=$USER
WorkingDirectory=$APP_DIR
ExecStart=$APP_DIR/Api_Celero
Restart=always
RestartSec=10
SyslogIdentifier=api-celero
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:$APP_PORT
Environment=PORT=$APP_PORT
EnvironmentFile=$APP_DIR/.env

# Logs
StandardOutput=journal
StandardError=journal

# Límites de recursos
LimitNOFILE=1048576
LimitNPROC=1048576

[Install]
WantedBy=multi-user.target
EOF

print_success "Servicio systemd configurado"

# Configurar nginx (usando la configuración existente de celero.network)
print_step "Configurando nginx para api.celero.network"
sudo tee /etc/nginx/sites-available/api-celero > /dev/null <<EOF
# Api Celero - Configuración Nginx
server {
    listen 80;
    server_name api.celero.network;
    
    # Redireccionar HTTP a HTTPS
    return 301 https://\$server_name\$request_uri;
}

server {
    listen 443 ssl http2;
    server_name api.celero.network;

    # Configuración SSL - estos certificados ya están gestionados por otro proceso
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

    # Configuración del proxy
    location / {
        proxy_pass http://127.0.0.1:$APP_PORT;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
        
        # Tamaño máximo del cuerpo de la petición
        client_max_body_size 10M;
    }

    # Logs específicos para la API
    access_log /var/log/nginx/api-celero.access.log;
    error_log /var/log/nginx/api-celero.error.log;
}
EOF

# Habilitar el sitio
sudo ln -sf /etc/nginx/sites-available/api-celero /etc/nginx/sites-enabled/
print_success "Configuración de nginx creada"

# Probar configuración de nginx
print_step "Verificando configuración de nginx"
if sudo nginx -t; then
    print_success "Configuración de nginx válida"
else
    print_error "Error en la configuración de nginx"
    exit 1
fi

# Habilitar y iniciar servicios
print_step "Iniciando servicios"
sudo systemctl daemon-reload
sudo systemctl enable $SERVICE_NAME
sudo systemctl start $SERVICE_NAME
sudo systemctl reload nginx
print_success "Servicios iniciados"

# Verificar estado de los servicios
print_step "Verificando estado de los servicios"
sleep 5

if systemctl is-active --quiet $SERVICE_NAME; then
    print_success "✅ Api_Celero está ejecutándose"
else
    print_error "❌ Api_Celero no está ejecutándose"
    echo "Ver logs con: sudo journalctl -u $SERVICE_NAME -f"
fi

if systemctl is-active --quiet nginx; then
    print_success "✅ Nginx está ejecutándose"
else
    print_error "❌ Nginx no está ejecutándose"
fi

# Mostrar información final
print_step "🎉 DESPLIEGUE COMPLETADO"
echo ""
echo "📍 Aplicación desplegada en: $APP_DIR"
echo "🌐 URL: https://api.celero.network"
echo "🔧 Puerto interno: $APP_PORT"
echo "📊 Estado del servicio: sudo systemctl status $SERVICE_NAME"
echo "📋 Logs en tiempo real: sudo journalctl -u $SERVICE_NAME -f"
echo "🔄 Reiniciar servicio: sudo systemctl restart $SERVICE_NAME"
echo ""
echo "🧪 PRUEBAS:"
echo "curl -k https://api.celero.network/api/clientes"
echo "curl -k https://api.celero.network/openapi/v1.json"
echo ""

print_success "¡Despliegue exitoso! 🚀"
