#!/bin/bash

# Actualizar configuración de Nginx para producción

cat > nginx-prod.conf << 'EOF'
server {
    listen 80;
    server_name api.tudominio.com;  # Cambiar por tu dominio

    # Redirección a HTTPS
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name api.tudominio.com;  # Cambiar por tu dominio

    # Certificados SSL (Let's Encrypt)
    ssl_certificate /etc/letsencrypt/live/api.tudominio.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.tudominio.com/privkey.pem;

    # Configuración SSL
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;

    # Headers de seguridad
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

    # Logs
    access_log /var/log/nginx/api-celero-access.log;
    error_log /var/log/nginx/api-celero-error.log;

    # Tamaño máximo de upload
    client_max_body_size 50M;

    # Proxy a la aplicación
    location / {
        proxy_pass http://localhost:7262;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;

        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }

    # Health check endpoint
    location /health {
        proxy_pass http://localhost:7262/health;
        access_log off;
    }
}
EOF

echo "✅ Archivo nginx-prod.conf creado"
echo "📝 Recuerda:"
echo "1. Cambiar 'api.tudominio.com' por tu dominio real"
echo "2. Instalar certificados SSL con Let's Encrypt"
echo "3. Copiar este archivo al servidor"