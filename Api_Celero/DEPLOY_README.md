# 🚀 Guía de Despliegue - API Celero

Esta guía te ayudará a desplegar la API Celero en un servidor Ubuntu.

## 📋 Prerrequisitos en Ubuntu

### 1. Instalar .NET 9 Runtime
```bash
# Agregar repositorio de Microsoft
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Actualizar e instalar .NET 9
sudo apt update
sudo apt install -y aspnetcore-runtime-9.0
```

### 2. Instalar dependencias del sistema
```bash
sudo apt update
sudo apt install -y nginx certbot python3-certbot-nginx
```

## 🛠️ Métodos de Despliegue

### Método 1: Despliegue Directo (Recomendado)

#### Paso 1: Compilar en Windows
```cmd
# Ejecutar en el directorio del proyecto
publish.bat
```

#### Paso 2: Subir archivos al servidor
```bash
# Crear directorio en el servidor
sudo mkdir -p /var/www/api-celero

# Subir archivos (usando scp, rsync, o tu método preferido)
scp -r publish/* usuario@servidor:/tmp/api-celero/
scp deploy.sh usuario@servidor:/tmp/
```

#### Paso 3: Ejecutar despliegue
```bash
# En el servidor Ubuntu
cd /tmp
sudo bash deploy.sh
```

#### Paso 4: Configurar variables de entorno
```bash
# Editar el archivo de servicio
sudo systemctl edit api-celero.service

# Agregar las variables de entorno:
[Service]
Environment=GOOGLE_RECAPTCHA_PROJECT_ID=tu-project-id
Environment=GOOGLE_RECAPTCHA_API_KEY=tu-api-key
Environment=GOOGLE_RECAPTCHA_SITE_KEY=tu-site-key
Environment=RESEND_API_KEY=tu-resend-key
Environment=RESEND_FROM_EMAIL=tu-email@dominio.com
Environment=RESEND_FROM_NAME=Tu Nombre

# Reiniciar el servicio
sudo systemctl daemon-reload
sudo systemctl restart api-celero.service
```

### Método 2: Despliegue con Docker

#### Paso 1: Construir imagen
```bash
# En el servidor, clonar el código
git clone tu-repositorio
cd Api_Celero

# Construir y ejecutar
docker-compose up -d
```

#### Paso 2: Configurar variables de entorno
```bash
# Crear archivo .env
cp .env.example .env
nano .env

# Editar con tus valores reales
# Luego reiniciar
docker-compose down && docker-compose up -d
```

## 🌐 Configuración de Nginx (Proxy Reverso)

### Crear configuración de Nginx
```bash
sudo nano /etc/nginx/sites-available/api-celero
```

```nginx
server {
    listen 80;
    server_name tu-dominio.com;
    
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

### Habilitar sitio
```bash
sudo ln -s /etc/nginx/sites-available/api-celero /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

### Configurar HTTPS con Certbot
```bash
sudo certbot --nginx -d tu-dominio.com
```

## 🔧 Comandos Útiles

### Gestión del servicio
```bash
# Ver estado
sudo systemctl status api-celero.service

# Ver logs
sudo journalctl -u api-celero.service -f

# Reiniciar
sudo systemctl restart api-celero.service

# Detener
sudo systemctl stop api-celero.service
```

### Monitoreo
```bash
# Ver procesos
ps aux | grep Api_Celero

# Ver puertos en uso
sudo netstat -tlnp | grep :7262

# Ver logs de Nginx
sudo tail -f /var/log/nginx/access.log
sudo tail -f /var/log/nginx/error.log
```

## 🔐 Seguridad

### Firewall
```bash
# Permitir puertos necesarios
sudo ufw allow ssh
sudo ufw allow 'Nginx Full'
sudo ufw enable
```

### Actualizaciones
```bash
# Mantener el sistema actualizado
sudo apt update && sudo apt upgrade -y

# Actualizar .NET
sudo apt update && sudo apt install aspnetcore-runtime-9.0
```

## 🚨 Troubleshooting

### La API no inicia
1. Verificar logs: `sudo journalctl -u api-celero.service -n 50`
2. Verificar permisos: `ls -la /var/www/api-celero/`
3. Verificar variables de entorno en el servicio

### Error 502 Bad Gateway
1. Verificar que la API esté ejecutándose: `sudo systemctl status api-celero.service`
2. Verificar configuración de Nginx: `sudo nginx -t`
3. Verificar que el puerto 5000 esté en uso: `sudo netstat -tlnp | grep :5000`

### Problemas de CORS
1. Verificar variable `ALLOWED_ORIGINS` en el servicio
2. Asegurar que el dominio frontend esté incluido

## 📞 Soporte

Para soporte adicional, revisar:
- Logs del servicio: `sudo journalctl -u api-celero.service -f`
- Logs de Nginx: `sudo tail -f /var/log/nginx/error.log`
- Estado del sistema: `sudo systemctl status api-celero.service`
