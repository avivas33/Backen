#!/bin/bash

# Script para preparar archivos para despliegue en servidor Linux

echo "📦 Preparando archivos para despliegue en servidor Linux..."

# Crear directorio de despliegue
DEPLOY_DIR="api-celero-deploy"
rm -rf $DEPLOY_DIR
mkdir -p $DEPLOY_DIR

# Copiar archivos necesarios
echo "📋 Copiando archivos necesarios..."

# Archivos de Docker
cp -r Api_Celero/Dockerfile $DEPLOY_DIR/
cp -r Api_Celero/docker-compose.yml $DEPLOY_DIR/
cp -r Api_Celero/.dockerignore $DEPLOY_DIR/

# Código fuente
mkdir -p $DEPLOY_DIR/src
cp -r Api_Celero/*.cs $DEPLOY_DIR/src/
cp -r Api_Celero/*.csproj $DEPLOY_DIR/src/
cp -r Api_Celero/appsettings*.json $DEPLOY_DIR/src/
cp -r Api_Celero/Controllers $DEPLOY_DIR/src/
cp -r Api_Celero/Models $DEPLOY_DIR/src/
cp -r Api_Celero/Services $DEPLOY_DIR/src/
cp -r Api_Celero/Migrations $DEPLOY_DIR/src/
cp -r Api_Celero/Utils $DEPLOY_DIR/src/
cp -r Api_Celero/Properties $DEPLOY_DIR/src/

# Scripts y configuración
cp .env.example $DEPLOY_DIR/
cp docker-*.sh $DEPLOY_DIR/
cp Api_Celero/nginx.conf $DEPLOY_DIR/
cp Api_Celero/api-celero.service $DEPLOY_DIR/

# Crear archivo de instalación
cat > $DEPLOY_DIR/install-on-server.sh << 'EOF'
#!/bin/bash

# Script de instalación para servidor Linux

echo "🚀 Instalando Api Celero en servidor Linux..."

# Verificar requisitos
command -v docker >/dev/null 2>&1 || { echo "❌ Docker no está instalado. Por favor instala Docker primero."; exit 1; }
command -v docker-compose >/dev/null 2>&1 || { echo "❌ Docker Compose no está instalado. Por favor instala Docker Compose primero."; exit 1; }

# Crear estructura de directorios
echo "📁 Creando estructura de directorios..."
sudo mkdir -p /opt/api-celero
sudo mkdir -p /opt/api-celero/data
sudo mkdir -p /opt/api-celero/logs

# Copiar archivos
echo "📋 Copiando archivos..."
sudo cp -r * /opt/api-celero/
sudo cp src/* /opt/api-celero/
sudo cp -r src/Controllers /opt/api-celero/
sudo cp -r src/Models /opt/api-celero/
sudo cp -r src/Services /opt/api-celero/
sudo cp -r src/Migrations /opt/api-celero/
sudo cp -r src/Utils /opt/api-celero/
sudo cp -r src/Properties /opt/api-celero/

# Configurar permisos
echo "🔐 Configurando permisos..."
sudo chown -R $USER:$USER /opt/api-celero
sudo chmod -R 755 /opt/api-celero
sudo chmod -R 777 /opt/api-celero/data
sudo chmod -R 777 /opt/api-celero/logs

# Configurar variables de entorno
if [ ! -f "/opt/api-celero/.env" ]; then
    echo "📝 Configurando variables de entorno..."
    sudo cp /opt/api-celero/.env.example /opt/api-celero/.env
    echo "⚠️  Por favor edita /opt/api-celero/.env con tus valores"
fi

# Hacer ejecutables los scripts
sudo chmod +x /opt/api-celero/*.sh

echo "✅ Instalación completada!"
echo ""
echo "📍 Ubicación: /opt/api-celero"
echo ""
echo "🔧 Próximos pasos:"
echo "1. Editar variables de entorno: sudo nano /opt/api-celero/.env"
echo "2. Construir imagen: cd /opt/api-celero && sudo ./docker-build.sh"
echo "3. Ejecutar aplicación: cd /opt/api-celero && sudo ./docker-run.sh"
EOF

chmod +x $DEPLOY_DIR/install-on-server.sh

# Crear README de despliegue
cat > $DEPLOY_DIR/README-DEPLOY.md << 'EOF'
# Guía de Despliegue en Servidor Linux

## Requisitos del Servidor

- Ubuntu 20.04+ o CentOS 8+
- Docker y Docker Compose instalados
- Mínimo 2GB RAM
- 10GB espacio en disco
- Puerto 7262 disponible (o el que prefieras)

## Instalación de Docker (si no está instalado)

### Ubuntu/Debian:
```bash
# Actualizar paquetes
sudo apt update
sudo apt upgrade -y

# Instalar Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Instalar Docker Compose
sudo apt install docker-compose -y

# Agregar usuario al grupo docker
sudo usermod -aG docker $USER
newgrp docker
```

### CentOS/RHEL:
```bash
# Instalar Docker
sudo yum install -y yum-utils
sudo yum-config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo
sudo yum install docker-ce docker-ce-cli containerd.io docker-compose-plugin -y

# Iniciar Docker
sudo systemctl start docker
sudo systemctl enable docker
```

## Pasos de Despliegue

1. **Subir archivos al servidor**:
   ```bash
   # Desde tu máquina local
   scp -r api-celero-deploy/ usuario@servidor:/home/usuario/
   ```

2. **Conectar al servidor**:
   ```bash
   ssh usuario@servidor
   ```

3. **Ejecutar instalación**:
   ```bash
   cd api-celero-deploy
   sudo ./install-on-server.sh
   ```

4. **Configurar variables de entorno**:
   ```bash
   sudo nano /opt/api-celero/.env
   ```

5. **Construir y ejecutar**:
   ```bash
   cd /opt/api-celero
   sudo ./docker-build.sh
   sudo ./docker-run.sh
   ```

## Configuración de Firewall

### UFW (Ubuntu):
```bash
sudo ufw allow 7262/tcp
sudo ufw reload
```

### Firewalld (CentOS):
```bash
sudo firewall-cmd --permanent --add-port=7262/tcp
sudo firewall-cmd --reload
```

## Configurar como Servicio Systemd (Opcional)

```bash
# Crear servicio
sudo cp /opt/api-celero/api-celero.service /etc/systemd/system/

# Recargar systemd
sudo systemctl daemon-reload

# Habilitar servicio
sudo systemctl enable api-celero

# Iniciar servicio
sudo systemctl start api-celero

# Ver estado
sudo systemctl status api-celero
```

## Proxy Reverso con Nginx (Recomendado)

```bash
# Instalar Nginx
sudo apt install nginx -y

# Copiar configuración
sudo cp /opt/api-celero/nginx.conf /etc/nginx/sites-available/api-celero
sudo ln -s /etc/nginx/sites-available/api-celero /etc/nginx/sites-enabled/

# Probar configuración
sudo nginx -t

# Reiniciar Nginx
sudo systemctl restart nginx
```

## Monitoreo

### Ver logs:
```bash
# Logs de Docker
cd /opt/api-celero
sudo docker-compose logs -f

# Logs de aplicación
tail -f /opt/api-celero/logs/*.log
```

### Ver estado:
```bash
sudo docker-compose ps
```

### Ver uso de recursos:
```bash
sudo docker stats
```

## Backup

### Backup de base de datos:
```bash
# Crear backup
sudo cp /opt/api-celero/data/recibos_offline.db /backup/recibos_offline_$(date +%Y%m%d).db

# Restaurar backup
sudo cp /backup/recibos_offline_20240115.db /opt/api-celero/data/recibos_offline.db
```

## Actualización

1. **Detener servicio**:
   ```bash
   cd /opt/api-celero
   sudo ./docker-stop.sh
   ```

2. **Actualizar código**:
   ```bash
   # Copiar nuevos archivos
   ```

3. **Reconstruir y ejecutar**:
   ```bash
   sudo ./docker-build.sh
   sudo ./docker-run.sh
   ```

## Solución de Problemas

### Error: Puerto en uso
```bash
# Ver qué usa el puerto
sudo netstat -tlnp | grep 7262

# Matar proceso
sudo kill -9 <PID>
```

### Error: Sin espacio en disco
```bash
# Limpiar imágenes Docker antiguas
sudo docker system prune -a
```

### Error: Permisos
```bash
# Arreglar permisos
sudo chown -R $USER:$USER /opt/api-celero
sudo chmod -R 777 /opt/api-celero/data
```
EOF

# Crear archivo comprimido
echo "📦 Creando archivo comprimido..."
tar -czf api-celero-deploy.tar.gz $DEPLOY_DIR/

echo "✅ Archivos preparados exitosamente!"
echo ""
echo "📦 Archivo creado: api-celero-deploy.tar.gz"
echo ""
echo "📋 Contenido del paquete:"
echo "  - Código fuente"
echo "  - Dockerfile y docker-compose.yml"
echo "  - Scripts de instalación"
echo "  - Configuración de Nginx"
echo "  - Servicio Systemd"
echo "  - Documentación"
echo ""
echo "🚀 Para desplegar:"
echo "1. Sube api-celero-deploy.tar.gz al servidor"
echo "2. Descomprime: tar -xzf api-celero-deploy.tar.gz"
echo "3. Ejecuta: cd api-celero-deploy && sudo ./install-on-server.sh"