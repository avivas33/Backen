# Solución al problema de CORS en Api_Celero

## Problema detectado
La API desplegada en el servidor de producción está teniendo problemas con CORS cuando se accede desde el frontend en `https://selfservice-dev.celero.network`. Específicamente, los navegadores reportan el error:

```
No 'Access-Control-Allow-Origin' header is present on the requested resource
```

## Causas identificadas
1. Nginx no está preservando correctamente los encabezados CORS generados por la aplicación ASP.NET Core
2. Posible configuración incorrecta del tipo de servicio systemd (notify vs simple)
3. Configuración incompleta de los orígenes permitidos en `appsettings.Production.json`

## Solución implementada

### 1. Modificación de la configuración de Nginx
Se ha actualizado el archivo `nginx.conf` para asegurar que los encabezados CORS generados por ASP.NET Core se preserven correctamente. Las modificaciones principales incluyen:

```nginx
# IMPORTANTE: Preservar los encabezados CORS generados por ASP.NET Core
proxy_pass_request_headers on;
proxy_pass_header Access-Control-Allow-Origin;
proxy_pass_header Access-Control-Allow-Methods;
proxy_pass_header Access-Control-Allow-Headers;
proxy_pass_header Access-Control-Allow-Credentials;
```

### 2. Corrección del tipo de servicio systemd
Se ha cambiado el tipo de servicio de `notify` a `simple` en el archivo `api-celero.service`:

```ini
[Service]
Type=simple
```

### 3. Actualización de la lista de orígenes permitidos
Se ha actualizado la lista de orígenes permitidos en `appsettings.Production.json` para incluir todas las posibles URLs de desarrollo y producción:

```json
"AllowedOrigins": [
  "http://localhost:8080",
  "https://localhost:8080",
  "http://localhost:3000",
  "https://localhost:3000",
  "https://selfservice-dev.celero.network",
  "https://selfservice.celero.network",
  "https://celero.network",
  "https://api.celero.network"
],
```

## Pasos para aplicar los cambios

1. Se han creado los siguientes scripts para facilitar la implementación y verificación:

   - `aplicar-cors.sh`: Script para aplicar todos los cambios de configuración en el servidor
   - `verificar-cors.sh`: Script para verificar que los encabezados CORS funcionan correctamente
   - `prueba-cors.html`: Página web para probar CORS directamente desde un navegador

2. Para aplicar los cambios en el servidor de producción:

   ```bash
   # 1. Copiar los archivos actualizados al servidor
   scp nginx.conf api-celero.service appsettings.Production.json aplicar-cors.sh verificar-cors.sh prueba-cors.html usuario@servidor:/ruta/de/destino/

   # 2. Ejecutar el script de aplicación (requiere sudo)
   sudo ./aplicar-cors.sh

   # 3. Verificar que CORS funciona correctamente
   ./verificar-cors.sh http://localhost:8080
   ./verificar-cors.sh https://api.celero.network
   ```

3. Para verificar desde el frontend:
   - Abrir la consola del navegador en https://selfservice-dev.celero.network
   - Ejecutar: `fetch('https://api.celero.network/api/clientes/health').then(r => console.log(r.ok))`
   - Deberías ver 'true' como resultado si CORS está funcionando correctamente

4. Alternativamente, abrir `prueba-cors.html` en un navegador para una prueba más completa.

## Notas adicionales

- Si después de aplicar estos cambios aún hay problemas de CORS, es posible que el firewall o alguna otra configuración de red esté interfiriendo.
- Para mejorar la seguridad, se debe configurar HTTPS correctamente descomentando la sección correspondiente en el archivo `nginx.conf` después de obtener certificados SSL válidos.
- El archivo `prueba-cors.html` no debe dejarse accesible públicamente en producción, es solo para fines de prueba.
