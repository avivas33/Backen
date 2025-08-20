# 🚀 GUÍA DE PUBLICACIÓN - API CELERO

## 📍 **INFORMACIÓN DEL DESPLIEGUE**

- **Carpeta de destino**: `/home/avivas/selfservice/Api`
- **Certificados SSL**: Ya configurados para `celero.network`
- **Dominio de la API**: `api.celero.network`
- **Puerto interno**: `5001`
- **Frontend permitido**: `https://selfservice.celero.network` y `https://selfservice-dev.celero.network`

---

## 🎯 **PASOS PARA PUBLICAR**

### **Paso 1: Preparar el Entorno**

```bash
# Actualizar el sistema
sudo apt update && sudo apt upgrade -y

# Verificar que .NET 9 esté instalado
dotnet --version
# Debe mostrar 9.x.x

# Si no está instalado, instalar .NET 9
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update
sudo apt install -y dotnet-sdk-9.0
```

### **Paso 2: Verificar Nginx**

```bash
# Verificar estado de nginx
sudo systemctl status nginx

# Si no está instalado
sudo apt install -y nginx
sudo systemctl enable nginx
sudo systemctl start nginx
```

### **Paso 3: Ejecutar el Script de Despliegue**

```bash
# Ir al directorio del proyecto
cd /home/avivas/Documentos/Api_Celero/Api_Celero

# Dar permisos de ejecución al script
chmod +x deploy-production.sh

# Ejecutar el despliegue
./deploy-production.sh
```

---

## ⚙️ **LO QUE HACE EL SCRIPT AUTOMÁTICAMENTE**

### ✅ **Compilación y Despliegue**
- Compila la aplicación para Linux x64
- Copia los archivos a `/home/avivas/selfservice/Api`
- Crea backup de la versión anterior
- Configura permisos correctos

### ✅ **Configuración de Servicios**
- Crea servicio systemd `api-celero`
- Configura nginx como proxy reverso
- Utiliza los certificados SSL existentes
- Configura CORS para los dominios del frontend

### ✅ **Variables de Entorno**
- Configura modo Production
- Establece puerto 5001
- Configura APIs de Resend y Google reCAPTCHA
- Define orígenes permitidos para CORS

---

## 🌐 **CONFIGURACIÓN DE NGINX**

El script crea automáticamente:

```nginx
server {
    listen 443 ssl http2;
    server_name api.celero.network;

    # Utiliza tus certificados existentes
    ssl_certificate /etc/letsencrypt/live/celero.network/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/celero.network/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:5001;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

---

## 🔧 **COMANDOS DE ADMINISTRACIÓN**

### **Estado del Servicio**
```bash
sudo systemctl status api-celero
```

### **Ver Logs en Tiempo Real**
```bash
sudo journalctl -u api-celero -f
```

### **Reiniciar Servicio**
```bash
sudo systemctl restart api-celero
```

### **Reiniciar Nginx**
```bash
sudo systemctl reload nginx
```

---

## 🧪 **PRUEBAS DESPUÉS DEL DESPLIEGUE**

### **1. Verificar que la API responde**
```bash
curl -k https://api.celero.network/api/clientes
```

### **2. Verificar documentación OpenAPI**
```bash
curl -k https://api.celero.network/openapi/v1.json
```

### **3. Probar desde el navegador**
```
https://api.celero.network/api/clientes
```

---

## 🚨 **SOLUCIÓN DE PROBLEMAS**

### **Si el servicio no inicia:**
```bash
# Ver logs detallados
sudo journalctl -u api-celero --no-pager

# Verificar permisos
ls -la /home/avivas/selfservice/Api/

# Verificar puerto en uso
sudo netstat -tlnp | grep :5001
```

### **Si nginx no puede acceder:**
```bash
# Verificar configuración
sudo nginx -t

# Ver logs de nginx
sudo tail -f /var/log/nginx/error.log
```

### **Para revisar configuración CORS:**
```bash
# Ver archivo de configuración
cat /home/avivas/selfservice/Api/appsettings.Production.json
```

---

## 🔄 **PROCESO DE ACTUALIZACIÓN**

Para futuras actualizaciones, simplemente ejecuta:

```bash
cd /home/avivas/Documentos/Api_Celero/Api_Celero
./deploy-production.sh
```

El script automáticamente:
- Crea backup de la versión actual
- Detiene el servicio
- Despliega la nueva versión
- Reinicia todos los servicios

---

## 📊 **MONITOREO**

### **Logs de la Aplicación**
```bash
sudo journalctl -u api-celero -f --since "1 hour ago"
```

### **Logs de Nginx**
```bash
sudo tail -f /var/log/nginx/api-celero.access.log
sudo tail -f /var/log/nginx/api-celero.error.log
```

### **Estado de Recursos**
```bash
# Uso de CPU y memoria
top
htop

# Espacio en disco
df -h
```

---

## ✅ **CHECKLIST POST-DESPLIEGUE**

- [ ] API responde en `https://api.celero.network`
- [ ] Servicio `api-celero` está activo
- [ ] Nginx está configurado correctamente
- [ ] Certificados SSL funcionan
- [ ] CORS permite el frontend
- [ ] Logs no muestran errores críticos
- [ ] Endpoints principales responden correctamente

---

¡Listo! Con estos pasos tendrás tu API Celero publicada y funcionando en producción. 🎉
