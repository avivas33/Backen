# 🚀 Guía de Despliegue - Api_Celero en Ubuntu

Esta guía te ayudará a desplegar la API Celero en un servidor Ubuntu con nginx como proxy reverso y SSL.

## 📋 Requisitos Previos

### En el Servidor Ubuntu:
- Ubuntu 20.04 LTS o superior
- Acceso sudo
- Dominio configurado apuntando al servidor
- Puertos 80 y 443 abiertos

### Dependencias Necesarias:
```bash
# Actualizar sistema
sudo apt update && sudo apt upgrade -y

# Instalar .NET 9
wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update
sudo apt install -y dotnet-sdk-9.0

# Instalar nginx
sudo apt install -y nginx

# Instalar certbot para SSL
sudo apt install -y certbot python3-certbot-nginx

# Verificar servicios
sudo systemctl enable nginx
sudo systemctl start nginx
```

## 🎯 Configuración para el Frontend

La API está preconfigurada para trabajar con:
- **Frontend Dev**: `https://selfservice-dev.celero.network`
- **Frontend Prod**: `https://selfservice.celero.network`

### Endpoints de la API:
- `POST /api/clientes/verificar-recaptcha` - Verificar reCAPTCHA
- `POST /api/clientes/enviar-email` - Enviar emails
- `GET /api/clientes/info` - Información del cliente

## 🚀 Despliegue Automático

### Opción 1: Script Completo (Recomendado)
```bash
# En tu máquina local, compilar y subir al servidor
dotnet publish -c Release -r linux-x64 --self-contained true

# Subir archivos al servidor
scp -r ./bin/Release/net9.0/linux-x64/publish/* usuario@servidor:/tmp/api-celero/
scp deploy-complete.sh usuario@servidor:/tmp/
scp nginx.conf usuario@servidor:/tmp/
scp api-celero.service usuario@servidor:/tmp/

# En el servidor
cd /tmp
chmod +x deploy-complete.sh
sudo ./deploy-complete.sh tu-dominio.com
```

### Opción 2: Paso a Paso

#### 1. Preparar Directorios
```bash
sudo mkdir -p /var/www/api-celero/{publish,logs,backups}
sudo chown -R www-data:www-data /var/www/api-celero
```

#### 2. Copiar Archivos Publicados
```bash
sudo cp -r /tmp/api-celero/* /var/www/api-celero/publish/
sudo chmod +x /var/www/api-celero/publish/Api_Celero
```

#### 3. Configurar Variables de Entorno
```bash
sudo nano /var/www/api-celero/.env
```

Contenido del archivo `.env`:
```bash
# Configuración de la aplicación
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5000

# Google reCAPTCHA Enterprise
GOOGLE_RECAPTCHA_PROJECT_ID=celero-apps
GOOGLE_RECAPTCHA_API_KEY=tu_api_key_real
GOOGLE_RECAPTCHA_SITE_KEY=tu_site_key_real

# Resend Email Service
RESEND_API_KEY=tu_resend_api_key_real
RESEND_FROM_EMAIL=noreply@celero.network
RESEND_FROM_NAME=Celero Network

# CORS - Dominios permitidos
ALLOWED_ORIGINS=https://selfservice-dev.celero.network,https://selfservice.celero.network
```

#### 4. Configurar Systemd Service
```bash
sudo nano /etc/systemd/system/api-celero.service
```

```ini
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
EnvironmentFile=/var/www/api-celero/.env

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable api-celero
sudo systemctl start api-celero
```

#### 5. Configurar Nginx
```bash
sudo nano /etc/nginx/sites-available/api-celero
```

Ver contenido en `nginx.conf` del proyecto.

```bash
sudo ln -s /etc/nginx/sites-available/api-celero /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

#### 6. Configurar SSL
```bash
sudo certbot --nginx -d tu-dominio.com
```

## 🔧 Verificación y Monitoreo

### Verificar Estado de Servicios
```bash
# Estado de la API
sudo systemctl status api-celero

# Ver logs en tiempo real
sudo journalctl -u api-celero -f

# Estado de nginx
sudo systemctl status nginx

# Logs de nginx
sudo tail -f /var/log/nginx/api-celero.access.log
sudo tail -f /var/log/nginx/api-celero.error.log
```

### Probar la API
```bash
# Verificar que la API responde
curl -X POST https://tu-dominio.com/api/clientes/verificar-recaptcha \
     -H "Content-Type: application/json" \
     -d '{"token":"test-token"}'

# Probar CORS desde el frontend
curl -X OPTIONS https://tu-dominio.com/api/clientes/verificar-recaptcha \
     -H "Origin: https://selfservice-dev.celero.network" \
     -H "Access-Control-Request-Method: POST" \
     -v
```

## 🛠 Comandos Útiles

### Gestión de Servicios
```bash
# Reiniciar API
sudo systemctl restart api-celero

# Recargar configuración de nginx
sudo systemctl reload nginx

# Ver configuración de nginx
sudo nginx -t

# Renovar certificado SSL
sudo certbot renew --dry-run
```

### Actualización de la API
```bash
# Detener servicio
sudo systemctl stop api-celero

# Actualizar archivos
sudo cp -r nuevos_archivos/* /var/www/api-celero/publish/

# Reiniciar servicio
sudo systemctl start api-celero
```

### Logs y Debugging
```bash
# Logs completos de la API
sudo journalctl -u api-celero --since "1 hour ago"

# Logs de nginx con filtro
sudo grep "POST /api/clientes" /var/log/nginx/api-celero.access.log

# Verificar uso de puertos
sudo netstat -tlnp | grep :5000
sudo netstat -tlnp | grep :80
sudo netstat -tlnp | grep :443
```

## 🔒 Seguridad

### Firewall
```bash
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
sudo ufw status
```

### Actualizaciones
```bash
# Actualizar sistema
sudo apt update && sudo apt upgrade -y

# Actualizar certificados
sudo certbot renew

# Verificar integridad de archivos
sudo find /var/www/api-celero -name "*.dll" -exec sha256sum {} \;
```

## 📞 Soporte

### Archivos de Configuración Importantes
- **API**: `/var/www/api-celero/`
- **Variables**: `/var/www/api-celero/.env`
- **Service**: `/etc/systemd/system/api-celero.service`
- **Nginx**: `/etc/nginx/sites-available/api-celero`
- **SSL**: `/etc/letsencrypt/live/tu-dominio.com/`

### Verificar Configuración CORS
La API está configurada para permitir solicitudes desde:
- ✅ `https://selfservice-dev.celero.network`
- ✅ `https://selfservice.celero.network`
- ✅ `https://celero.network`

Si necesitas agregar más dominios, edita:
1. `/var/www/api-celero/.env` (variable `ALLOWED_ORIGINS`)
2. `/etc/nginx/sites-available/api-celero` (headers CORS)

## 🎉 ¡Listo!

Tu API Celero ahora está ejecutándose en Ubuntu con:
- ✅ .NET 9 runtime
- ✅ Nginx como proxy reverso
- ✅ SSL/TLS con Let's Encrypt
- ✅ CORS configurado para el frontend
- ✅ Systemd para gestión de servicios
- ✅ Logs centralizados

**URL de la API**: `https://tu-dominio.com`
