# Instrucciones para Aplicar Correcciones CORS en Producción

## ✅ Preparación Completada

Los archivos ya están compilados y listos en el directorio `publish/`. 

## 📋 Pasos para el Servidor de Producción

### 1. Transferir Archivos al Servidor

Desde tu máquina local, ejecuta:

```bash
# Opción 1: Usando SCP (reemplaza USER y SERVER_IP)
scp -r /home/avivas/Documentos/Api_Celero/Api_Celero/publish/* USER@SERVER_IP:/path/to/api-celero/

# Opción 2: Usando rsync (más eficiente para actualizaciones)
rsync -avz /home/avivas/Documentos/Api_Celero/Api_Celero/publish/ USER@SERVER_IP:/path/to/api-celero/
```

También transfiere la configuración de nginx:
```bash
scp /home/avivas/Documentos/Api_Celero/Api_Celero/nginx-cors-fixed.conf USER@SERVER_IP:/tmp/
```

### 2. En el Servidor de Producción

Conéctate al servidor y ejecuta:

```bash
# Hacer backup del estado actual
sudo cp -r /path/to/api-celero /path/to/api-celero.backup.$(date +%Y%m%d)

# Detener el servicio
sudo systemctl stop api-celero

# Copiar los nuevos archivos (ajusta la ruta según tu configuración)
sudo cp -r /tmp/publish/* /path/to/api-celero/

# Dar permisos de ejecución
sudo chmod +x /path/to/api-celero/Api_Celero

# Actualizar configuración de nginx
sudo cp /etc/nginx/sites-available/api-celero /etc/nginx/sites-available/api-celero.backup
sudo cp /tmp/nginx-cors-fixed.conf /etc/nginx/sites-available/api-celero

# Verificar configuración de nginx
sudo nginx -t

# Si la verificación es exitosa, recargar nginx
sudo systemctl reload nginx

# Iniciar el servicio de la API
sudo systemctl start api-celero

# Verificar que esté funcionando
sudo systemctl status api-celero
```

### 3. Verificación de CORS

#### Prueba Rápida con curl:
```bash
curl -I -X OPTIONS https://api.celero.network/api/clientes \
  -H 'Origin: https://selfservice-dev.celero.network' \
  -H 'Access-Control-Request-Method: GET' \
  -H 'Access-Control-Request-Headers: Content-Type'
```

**Deberías ver estos headers:**
- `Access-Control-Allow-Origin: https://selfservice-dev.celero.network`
- `Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS, PATCH`
- `Access-Control-Allow-Headers: DNT,User-Agent,X-Requested-With,...`
- `Access-Control-Allow-Credentials: true`

#### Prueba desde el Frontend:
1. Ve a https://selfservice-dev.celero.network
2. Abre la consola del navegador (F12)
3. Intenta hacer la petición que antes fallaba
4. No deberías ver errores de CORS

### 4. Monitoreo de Logs

```bash
# Ver logs de la API
sudo journalctl -u api-celero -f

# Ver logs de nginx
sudo tail -f /var/log/nginx/api-celero.error.log
sudo tail -f /var/log/nginx/api-celero.access.log
```

## 🔧 Troubleshooting

### Si CORS sigue fallando:

1. **Verifica que nginx esté pasando los headers:**
```bash
# En el servidor, verifica la respuesta completa
curl -v -X OPTIONS http://localhost:8080/api/clientes \
  -H 'Origin: https://selfservice-dev.celero.network'
```

2. **Verifica los logs de la aplicación:**
```bash
sudo journalctl -u api-celero | grep -i cors
```

3. **Asegúrate de que el origen esté en la lista permitida:**
   - Revisa `/path/to/api-celero/appsettings.Production.json`
   - La sección `AllowedOrigins` debe incluir `https://selfservice-dev.celero.network`

4. **Si necesitas agregar más orígenes permitidos:**
   - Edita `appsettings.Production.json`
   - Agrega el nuevo origen a la lista `AllowedOrigins`
   - Reinicia el servicio: `sudo systemctl restart api-celero`

### Rollback si es necesario:

```bash
# Restaurar backup
sudo systemctl stop api-celero
sudo cp -r /path/to/api-celero.backup.FECHA/* /path/to/api-celero/
sudo cp /etc/nginx/sites-available/api-celero.backup /etc/nginx/sites-available/api-celero
sudo nginx -t && sudo systemctl reload nginx
sudo systemctl start api-celero
```

## 📝 Cambios Aplicados

1. **Program.cs**: Orden correcto del middleware CORS, política nombrada, logging mejorado
2. **nginx.conf**: Manejo correcto de peticiones OPTIONS, validación de orígenes
3. **Headers CORS**: Configurados para permitir credenciales y métodos necesarios

## ✅ Checklist Final

- [ ] Archivos transferidos al servidor
- [ ] Backup creado
- [ ] Servicio api-celero detenido
- [ ] Nuevos archivos copiados
- [ ] nginx actualizado y recargado
- [ ] Servicio api-celero iniciado
- [ ] Prueba con curl exitosa
- [ ] Prueba desde frontend exitosa
- [ ] Logs monitoreados sin errores