# 🚀 PASOS FINALES PARA COMPLETAR EL DESPLIEGUE

## ✅ **ESTADO ACTUAL**
- ✅ Aplicación compilada y desplegada en `/home/avivas/selfservice/Api`
- ✅ Configuración de producción creada
- ✅ Aplicación probada y funcionando en puerto 5001
- ✅ Archivos de configuración listos en `/tmp/`

## 🔧 **COMANDOS PARA EJECUTAR CON SUDO**

### **1. Instalar servicio systemd**
```bash
sudo cp /tmp/api-celero.service /etc/systemd/system/api-celero.service
sudo systemctl daemon-reload
sudo systemctl enable api-celero
```

### **2. Configurar nginx**
```bash
sudo cp /tmp/api-celero-nginx.conf /etc/nginx/sites-available/api-celero
sudo ln -sf /etc/nginx/sites-available/api-celero /etc/nginx/sites-enabled/
```

### **3. Verificar configuración de nginx**
```bash
sudo nginx -t
```

### **4. Iniciar servicios**
```bash
sudo systemctl start api-celero
sudo systemctl reload nginx
```

### **5. Verificar estado**
```bash
sudo systemctl status api-celero
sudo systemctl status nginx
```

## 📊 **VERIFICACIÓN POST-DESPLIEGUE**

### **Verificar que los servicios están activos:**
```bash
sudo systemctl is-active api-celero
sudo systemctl is-active nginx
```

### **Ver logs de la aplicación:**
```bash
sudo journalctl -u api-celero -f
```

### **Probar la API:**
```bash
curl https://api.celero.network/api/clientes
curl https://api.celero.network/openapi/v1.json
```

## 🌐 **URLS DE ACCESO**

- **API**: https://api.celero.network
- **Documentación**: https://api.celero.network/openapi/v1.json
- **Endpoint de prueba**: https://api.celero.network/api/clientes

## 🔄 **COMANDOS DE ADMINISTRACIÓN**

### **Reiniciar servicios:**
```bash
sudo systemctl restart api-celero
sudo systemctl reload nginx
```

### **Ver logs:**
```bash
# Logs de la aplicación
sudo journalctl -u api-celero -f

# Logs de nginx
sudo tail -f /var/log/nginx/api-celero.access.log
sudo tail -f /var/log/nginx/api-celero.error.log
```

### **Detener servicios:**
```bash
sudo systemctl stop api-celero
```

## 🎯 **PRÓXIMOS PASOS**

1. Ejecutar los comandos sudo listados arriba
2. Verificar que los servicios estén activos
3. Probar la API desde el navegador
4. Configurar monitoreo si es necesario

## 📋 **RESUMEN DEL DESPLIEGUE**

- **Directorio**: `/home/avivas/selfservice/Api`
- **Puerto interno**: 5001
- **Usuario del servicio**: avivas
- **Logs**: journalctl y /var/log/nginx/
- **Configuración**: /home/avivas/selfservice/Api/appsettings.Production.json

¡El despliegue está casi completo! Solo falta ejecutar los comandos sudo.
