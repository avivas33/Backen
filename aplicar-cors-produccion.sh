#!/bin/bash

echo "==================================="
echo "Aplicando correcciones CORS en Producción"
echo "==================================="

# Colores
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${YELLOW}Este script requiere permisos sudo${NC}"
echo ""

# 1. Backup actual
echo -e "${GREEN}[1/6]${NC} Creando backup del estado actual..."
sudo cp -r /var/www/api-celero/publish /var/www/api-celero/publish.backup.$(date +%Y%m%d_%H%M%S)
sudo cp /etc/nginx/sites-available/api-celero /etc/nginx/sites-available/api-celero.backup.$(date +%Y%m%d_%H%M%S)

# 2. Detener el servicio
echo -e "${GREEN}[2/6]${NC} Deteniendo servicio api-celero..."
sudo systemctl stop api-celero

# 3. Copiar nuevos archivos
echo -e "${GREEN}[3/6]${NC} Copiando archivos actualizados..."
sudo cp -r /home/avivas/Documentos/Api_Celero/Api_Celero/publish/* /var/www/api-celero/publish/
sudo chmod +x /var/www/api-celero/publish/Api_Celero

# 4. Actualizar nginx con configuración CORS corregida
echo -e "${GREEN}[4/6]${NC} Actualizando configuración de nginx..."
sudo tee /etc/nginx/sites-available/api-celero > /dev/null << 'EOF'
# Mapa de orígenes permitidos
map $http_origin $cors_origin {
    default "";
    "http://localhost:8080" "$http_origin";
    "https://localhost:8080" "$http_origin";
    "http://localhost:3000" "$http_origin";
    "https://localhost:3000" "$http_origin";
    "https://selfservice-dev.celero.network" "$http_origin";
    "https://selfservice.celero.network" "$http_origin";
    "https://celero.network" "$http_origin";
}

server {
    listen 80;
    server_name api.celero.network;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name api.celero.network;
    
    # SSL Configuration
    ssl_certificate /etc/letsencrypt/live/celero.network/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/celero.network/privkey.pem;
    
    ssl_session_timeout 1d;
    ssl_session_cache shared:MozTLS:10m;
    ssl_session_tickets off;
    
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384;
    ssl_prefer_server_ciphers off;
    
    # Security headers
    add_header Strict-Transport-Security "max-age=63072000" always;
    add_header X-Content-Type-Options nosniff;
    add_header X-Frame-Options DENY;
    add_header X-XSS-Protection "1; mode=block";
    
    location / {
        # Manejo especial para peticiones OPTIONS (preflight)
        if ($request_method = 'OPTIONS') {
            add_header 'Access-Control-Allow-Origin' $cors_origin always;
            add_header 'Access-Control-Allow-Methods' 'GET, POST, PUT, DELETE, OPTIONS, PATCH' always;
            add_header 'Access-Control-Allow-Headers' 'DNT,User-Agent,X-Requested-With,If-Modified-Since,Cache-Control,Content-Type,Range,Authorization' always;
            add_header 'Access-Control-Allow-Credentials' 'true' always;
            add_header 'Access-Control-Max-Age' 1728000;
            add_header 'Content-Type' 'text/plain; charset=utf-8';
            add_header 'Content-Length' 0;
            return 204;
        }
        
        # Proxy a la aplicación .NET
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        
        # Headers CORS para todas las respuestas
        add_header 'Access-Control-Allow-Origin' $cors_origin always;
        add_header 'Access-Control-Allow-Methods' 'GET, POST, PUT, DELETE, OPTIONS, PATCH' always;
        add_header 'Access-Control-Allow-Headers' 'DNT,User-Agent,X-Requested-With,If-Modified-Since,Cache-Control,Content-Type,Range,Authorization' always;
        add_header 'Access-Control-Allow-Credentials' 'true' always;
        
        # Timeouts
        proxy_connect_timeout 30s;
        proxy_send_timeout 30s;
        proxy_read_timeout 30s;
        
        # Buffer settings
        proxy_buffering on;
        proxy_buffer_size 128k;
        proxy_buffers 4 256k;
        proxy_busy_buffers_size 256k;
    }
    
    # Health check endpoint
    location /health {
        proxy_pass http://localhost:5000/health;
        access_log off;
    }
    
    # Logs
    access_log /var/log/nginx/api-celero.access.log;
    error_log /var/log/nginx/api-celero.error.log;
}
EOF

# 5. Verificar y recargar nginx
echo -e "${GREEN}[5/6]${NC} Verificando configuración de nginx..."
sudo nginx -t
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓${NC} Configuración válida. Recargando nginx..."
    sudo systemctl reload nginx
else
    echo -e "${RED}✗${NC} Error en configuración de nginx. Restaurando backup..."
    ULTIMO_BACKUP=$(ls -t /etc/nginx/sites-available/api-celero.backup.* | head -1)
    sudo cp $ULTIMO_BACKUP /etc/nginx/sites-available/api-celero
    sudo systemctl reload nginx
    exit 1
fi

# 6. Reiniciar servicio
echo -e "${GREEN}[6/6]${NC} Reiniciando servicio api-celero..."
sudo systemctl start api-celero
sleep 3

# Verificar estado
if sudo systemctl is-active --quiet api-celero; then
    echo -e "${GREEN}✓${NC} Servicio api-celero activo"
else
    echo -e "${RED}✗${NC} Error al iniciar api-celero. Verificando logs..."
    sudo journalctl -u api-celero -n 20
    exit 1
fi

echo ""
echo "==================================="
echo -e "${GREEN}✅ Correcciones CORS aplicadas exitosamente${NC}"
echo "==================================="
echo ""

# Test CORS
echo "Probando CORS..."
echo ""
curl -I -X OPTIONS https://api.celero.network/api/clientes \
    -H 'Origin: https://selfservice-dev.celero.network' \
    -H 'Access-Control-Request-Method: GET' \
    -H 'Access-Control-Request-Headers: Content-Type' 2>/dev/null | grep -i "access-control"

echo ""
echo "Si ves los headers Access-Control-* arriba, CORS está funcionando correctamente."
echo ""
echo "Prueba desde el frontend en: https://selfservice-dev.celero.network"