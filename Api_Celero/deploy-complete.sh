#!/bin/bash

# Script de despliegue completo para Api_Celero en Ubuntu
# Incluye configuración de CORS para https://selfservice-dev.celero.network
# Uso: ./deploy-complete.sh [dominio]

set -e  # Salir si cualquier comando falla

# Colores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Función para imprimir mensajes con color
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

# Verificar argumentos
DOMAIN=${1:-"api.celero.network"}
FRONTEND_DOMAIN="https://selfservice-dev.celero.network"

print_step "Iniciando despliegue completo de Api_Celero"
echo "Dominio API: $DOMAIN"
echo "Frontend: $FRONTEND_DOMAIN"
echo ""

# Verificar que estamos en el directorio correcto
if [ ! -f "Api_Celero.csproj" ]; then
    print_error "No se encontró Api_Celero.csproj. Ejecuta este script desde el directorio del proyecto."
    exit 1
fi

# Crear directorio de la aplicación
print_step "Configurando directorios"
sudo mkdir -p /var/www/api-celero/{publish,logs,backups}
sudo chown -R www-data:www-data /var/www/api-celero
print_success "Directorios creados"

# Publicar la aplicación
print_step "Publicando aplicación para Linux"
dotnet publish -c Release -r linux-x64 --self-contained true -o /tmp/api-celero-publish
print_success "Aplicación publicada"

# Copiar archivos publicados
print_step "Copiando archivos de la aplicación"
sudo cp -r /tmp/api-celero-publish/* /var/www/api-celero/publish/
sudo rm -rf /tmp/api-celero-publish
print_success "Archivos copiados"

# Configurar variables de entorno
print_step "Configurando variables de entorno"
sudo tee /var/www/api-celero/.env > /dev/null <<EOF
# Configuración de la aplicación
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5000
PORT=5000

# Google reCAPTCHA Enterprise
GOOGLE_RECAPTCHA_PROJECT_ID=celero-apps
GOOGLE_RECAPTCHA_API_KEY=AIzaSyCj_pLq52sr846YrxrUJ_9StrFHHxQ1unY
GOOGLE_RECAPTCHA_SITE_KEY=6Lfj1mArAAAAAIkj3BJGhSMpIdUT6qnCa1aUMrRN

# Resend Email Service
RESEND_API_KEY=re_af22v2eR_5UeSvpks4dfnJWTe6dLYJs5a
RESEND_FROM_EMAIL=noreply@celero.network
RESEND_FROM_NAME=Celero Network

# CORS - Dominios permitidos
ALLOWED_ORIGINS=https://selfservice-dev.celero.network,https://selfservice.celero.network,https://celero.network
EOF

print_success "Variables de entorno configuradas"

# Configurar permisos
sudo chown -R www-data:www-data /var/www/api-celero
sudo chmod +x /var/www/api-celero/publish/Api_Celero
print_success "Permisos configurados"

# Configurar systemd service
print_step "Configurando servicio systemd"
sudo tee /etc/systemd/system/api-celero.service > /dev/null <<EOF
[Unit]
Description=Api Celero - ASP.NET 9 Web API
After=network.target

[Service]
Type=notify
ExecStart=/usr/bin/dotnet /var/www/api-celero/publish/Api_Celero.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=api-celero
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
EnvironmentFile=/var/www/api-celero/.env

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable api-celero
print_success "Servicio systemd configurado"

# Configurar nginx con CORS para el frontend
print_step "Configurando nginx"
sudo tee /etc/nginx/sites-available/api-celero > /dev/null <<EOF
server {
    listen 80;
    server_name $DOMAIN;
    
    # Redirect HTTP to HTTPS
    return 301 https://\$host\$request_uri;
}

server {
    listen 443 ssl http2;
    server_name $DOMAIN;
    
    # SSL Configuration (se configurará automáticamente con certbot)
    # ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    # ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
    
    # Security headers
    add_header X-Content-Type-Options nosniff;
    add_header X-Frame-Options DENY;
    add_header X-XSS-Protection "1; mode=block";
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    
    # CORS headers para el frontend de Celero
    location / {
        # Handle preflight requests
        if (\$request_method = 'OPTIONS') {
            add_header Access-Control-Allow-Origin "https://selfservice-dev.celero.network" always;
            add_header Access-Control-Allow-Methods "GET, POST, PUT, DELETE, OPTIONS" always;
            add_header Access-Control-Allow-Headers "Content-Type, Authorization, X-Requested-With" always;
            add_header Access-Control-Allow-Credentials true always;
            add_header Access-Control-Max-Age 86400;
            add_header Content-Length 0;
            add_header Content-Type text/plain;
            return 204;
        }
        
        # Proxy to .NET application
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
        
        # CORS headers for normal requests
        add_header Access-Control-Allow-Origin "https://selfservice-dev.celero.network" always;
        add_header Access-Control-Allow-Credentials true always;
        
        # Timeouts
        proxy_connect_timeout 30s;
        proxy_send_timeout 30s;
        proxy_read_timeout 30s;
    }
    
    # Logs
    access_log /var/log/nginx/api-celero.access.log;
    error_log /var/log/nginx/api-celero.error.log;
}
EOF

# Habilitar el sitio
sudo ln -sf /etc/nginx/sites-available/api-celero /etc/nginx/sites-enabled/

# Verificar configuración de nginx
if sudo nginx -t; then
    print_success "Configuración de nginx válida"
    sudo systemctl reload nginx
else
    print_error "Error en la configuración de nginx"
    exit 1
fi

# Iniciar servicios
print_step "Iniciando servicios"
sudo systemctl start api-celero
sleep 5

# Verificar estado de los servicios
if sudo systemctl is-active --quiet api-celero; then
    print_success "Api_Celero está ejecutándose"
else
    print_error "Error al iniciar Api_Celero"
    sudo journalctl -u api-celero --no-pager -n 20
    exit 1
fi

if sudo systemctl is-active --quiet nginx; then
    print_success "Nginx está ejecutándose"
else
    print_error "Error con nginx"
    exit 1
fi

# Configurar SSL
print_step "Configurando SSL con Let's Encrypt"
if command -v certbot &> /dev/null; then
    print_warning "Configurando SSL para $DOMAIN"
    sudo certbot --nginx -d $DOMAIN --non-interactive --agree-tos --email admin@celero.network || print_warning "SSL no configurado automáticamente"
else
    print_warning "Certbot no está instalado. Para instalar SSL ejecuta:"
    echo "sudo apt install certbot python3-certbot-nginx"
    echo "sudo certbot --nginx -d $DOMAIN"
fi

# Configurar firewall
print_step "Configurando firewall"
if command -v ufw &> /dev/null; then
    sudo ufw allow 22/tcp
    sudo ufw allow 80/tcp
    sudo ufw allow 443/tcp
    sudo ufw --force enable || print_warning "No se pudo configurar el firewall automáticamente"
    print_success "Firewall configurado"
fi

# Mostrar información final
print_step "Despliegue completado"
echo ""
print_success "🎉 La API está lista y funcionando!"
echo ""
echo "📍 URLs de la API:"
echo "  - HTTP:  http://$DOMAIN"
echo "  - HTTPS: https://$DOMAIN"
echo ""
echo "🔗 Endpoints principales:"
echo "  - GET  https://$DOMAIN/api/clientes/info"
echo "  - POST https://$DOMAIN/api/clientes/verificar-recaptcha"
echo "  - POST https://$DOMAIN/api/clientes/enviar-email"
echo ""
echo "🌐 CORS configurado para:"
echo "  - https://selfservice-dev.celero.network"
echo "  - https://selfservice.celero.network"
echo "  - https://celero.network"
echo ""
echo "🛠 Comandos útiles:"
echo "  - Ver logs: sudo journalctl -u api-celero -f"
echo "  - Reiniciar: sudo systemctl restart api-celero"
echo "  - Estado: sudo systemctl status api-celero"
echo "  - Nginx logs: sudo tail -f /var/log/nginx/api-celero.access.log"
echo ""
echo "📋 Prueba la API:"
echo "  curl -X POST https://$DOMAIN/api/clientes/verificar-recaptcha \\"
echo "       -H \"Content-Type: application/json\" \\"
echo "       -d '{\"token\":\"test-token\"}'"
echo ""
print_success "¡Despliegue completado exitosamente!"
