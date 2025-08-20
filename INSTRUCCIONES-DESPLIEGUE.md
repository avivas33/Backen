# 📋 Instrucciones de Despliegue para Servidor Linux

## 🚀 Pasos Rápidos

### 1. Preparar archivos en tu máquina local
```bash
# Ejecutar script de preparación
chmod +x deploy-to-server.sh
./deploy-to-server.sh
```

Esto creará el archivo: `api-celero-deploy.tar.gz`

### 2. Subir archivo al servidor
```bash
# Opción 1: SCP
scp api-celero-deploy.tar.gz usuario@servidor:/home/usuario/

# Opción 2: SFTP
sftp usuario@servidor
put api-celero-deploy.tar.gz
exit

# Opción 3: rsync
rsync -avz api-celero-deploy.tar.gz usuario@servidor:/home/usuario/
```

### 3. En el servidor Linux

#### Conectar al servidor:
```bash
ssh usuario@servidor
```

#### Descomprimir y ejecutar:
```bash
# Descomprimir
tar -xzf api-celero-deploy.tar.gz

# Entrar al directorio
cd api-celero-deploy

# Ejecutar instalación
sudo ./install-on-server.sh
```

### 4. Configurar variables de entorno
```bash
# Editar archivo .env
sudo nano /opt/api-celero/.env
```

Variables importantes a configurar:
- `RESEND_API_KEY` - Tu API key de Resend
- `HANSA_BASE_URL` - URL de la API Hansa
- `HANSA_USUARIO` - Usuario Hansa
- `HANSA_CLAVE` - Contraseña Hansa
- `COBALT_CLIENT_ID` - Client ID de Cobalt
- `COBALT_CLIENT_SECRET` - Client Secret de Cobalt
- `PAYPAL_CLIENT_ID` - Client ID de PayPal
- `PAYPAL_CLIENT_SECRET` - Client Secret de PayPal

### 5. Construir y ejecutar
```bash
cd /opt/api-celero
sudo ./docker-build.sh
sudo ./docker-run.sh
```

## 📦 Archivos que se incluyen

El paquete `api-celero-deploy.tar.gz` contiene:

1. **Código fuente**:
   - Todos los archivos .cs
   - Controllers, Models, Services
   - Migraciones de base de datos
   - Archivos de configuración

2. **Docker**:
   - Dockerfile
   - docker-compose.yml
   - .dockerignore

3. **Scripts**:
   - docker-build.sh
   - docker-run.sh
   - docker-stop.sh
   - install-on-server.sh

4. **Configuración**:
   - nginx.conf (para proxy reverso)
   - api-celero.service (servicio systemd)
   - .env.example

5. **Documentación**:
   - README-DEPLOY.md

## 🔒 Configuración de Seguridad

### 1. Firewall
```bash
# Ubuntu/Debian
sudo ufw allow 7262/tcp
sudo ufw reload

# CentOS/RHEL
sudo firewall-cmd --permanent --add-port=7262/tcp
sudo firewall-cmd --reload
```

### 2. SSL con Let's Encrypt
```bash
# Instalar Certbot
sudo apt install certbot python3-certbot-nginx -y

# Obtener certificado
sudo certbot --nginx -d api.tudominio.com
```

### 3. Configurar Nginx (Opcional pero recomendado)
```bash
# Copiar configuración
sudo cp nginx-prod.conf /etc/nginx/sites-available/api-celero
sudo ln -s /etc/nginx/sites-available/api-celero /etc/nginx/sites-enabled/

# Editar dominio
sudo nano /etc/nginx/sites-available/api-celero

# Probar y reiniciar
sudo nginx -t
sudo systemctl restart nginx
```

## 🔧 Administración

### Ver logs:
```bash
# Logs de Docker
cd /opt/api-celero
sudo docker-compose logs -f

# Logs de aplicación
sudo tail -f /opt/api-celero/logs/*.log
```

### Reiniciar servicio:
```bash
cd /opt/api-celero
sudo docker-compose restart
```

### Detener servicio:
```bash
cd /opt/api-celero
sudo ./docker-stop.sh
```

### Actualizar aplicación:
```bash
# 1. Detener servicio actual
sudo ./docker-stop.sh

# 2. Subir nuevos archivos
# (repetir proceso de subida)

# 3. Reconstruir y ejecutar
sudo ./docker-build.sh
sudo ./docker-run.sh
```

## 🆘 Solución de Problemas

### Error: Docker no instalado
```bash
# Instalar Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
```

### Error: Puerto 7262 en uso
```bash
# Ver qué usa el puerto
sudo lsof -i :7262

# Cambiar puerto en docker-compose.yml
```

### Error: Sin permisos
```bash
# Arreglar permisos
sudo chown -R $USER:$USER /opt/api-celero
sudo chmod -R 777 /opt/api-celero/data
```

## 📊 Monitoreo

### Estado del contenedor:
```bash
sudo docker ps
sudo docker stats api-celero
```

### Espacio en disco:
```bash
df -h
du -sh /opt/api-celero/*
```

### Uso de memoria:
```bash
free -m
htop
```

## 🔄 Backup

### Backup manual:
```bash
# Crear backup
sudo tar -czf /backup/api-celero-$(date +%Y%m%d).tar.gz /opt/api-celero/data

# Base de datos SQLite
sudo cp /opt/api-celero/data/recibos_offline.db /backup/
```

### Backup automático (cron):
```bash
# Editar crontab
sudo crontab -e

# Agregar línea (backup diario a las 2 AM)
0 2 * * * tar -czf /backup/api-celero-$(date +\%Y\%m\%d).tar.gz /opt/api-celero/data
```

## 📱 Contacto y Soporte

Si tienes problemas durante el despliegue:
1. Revisa los logs: `sudo docker-compose logs`
2. Verifica las variables de entorno: `cat /opt/api-celero/.env`
3. Asegúrate que Docker esté corriendo: `sudo systemctl status docker`