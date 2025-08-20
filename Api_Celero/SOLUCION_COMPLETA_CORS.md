# Solución al problema de CORS en Api_Celero

## Problema detectado
Cuando se accede a la API desde `https://selfservice-dev.celero.network`, se produce el siguiente error:

```
Access to fetch at 'https://api.celero.network/api/clientes?range=20037' from origin 'https://selfservice-dev.celero.network' has been blocked by CORS policy: No 'Access-Control-Allow-Origin' header is present on the requested resource
```

## Posibles causas identificadas

1. **Configuración HTTPS incompleta**: Aunque la API se está sirviendo a través de HTTPS, los certificados SSL podrían no estar correctamente configurados.

2. **Conflicto entre Nginx y ASP.NET Core**: Nginx podría estar eliminando los encabezados CORS que ASP.NET Core está intentando enviar.

3. **Configuración de puertos incorrecta**: Puede haber una discrepancia entre el puerto que Nginx espera y el puerto en el que la API está escuchando.

4. **Problemas con el servicio systemd**: La configuración del servicio systemd podría estar incompleta o incorrecta.

## Solución implementada

Se ha creado un script integral `fix-cors.sh` que implementa las siguientes soluciones:

### 1. Configuración HTTPS correcta

- Se ha activado la redirección de HTTP a HTTPS en Nginx
- Se han configurado correctamente los certificados SSL
- Se han aplicado configuraciones de seguridad SSL recomendadas

### 2. Doble capa de protección CORS

- La API ASP.NET Core sigue gestionando los encabezados CORS según la configuración en `Program.cs`
- Como medida adicional, Nginx también envía encabezados CORS para las peticiones que gestiona
- Se ha añadido un manejo especial para las peticiones OPTIONS (preflight) en Nginx

### 3. Corrección de configuración de puertos

- Se ha establecido explícitamente que la API escuche en 0.0.0.0:8080
- Se ha asegurado que esta configuración esté presente tanto en el servicio systemd como en el archivo .env

### 4. Mejoras en el servicio systemd

- Se ha corregido el tipo de servicio a "simple"
- Se ha añadido la carga de variables de entorno desde un archivo .env
- Se han establecido explícitamente permisos para los archivos críticos

## Instrucciones para aplicar la solución

1. Copia el script `fix-cors.sh` al servidor
2. Haz el script ejecutable: `chmod +x fix-cors.sh`
3. Ejecuta el script con permisos de administrador: `sudo ./fix-cors.sh`
4. Verifica que los servicios estén funcionando: 
   ```
   systemctl status api-celero
   systemctl status nginx
   ```
5. Prueba el acceso desde el frontend en `https://selfservice-dev.celero.network`

## Verificación y solución de problemas

Si después de aplicar estas correcciones sigues teniendo problemas:

1. **Verifica los registros de Nginx**:
   ```
   tail -f /var/log/nginx/api-celero.error.log
   ```

2. **Verifica los registros del servicio**:
   ```
   journalctl -u api-celero -f
   ```

3. **Comprueba la sintaxis de la configuración de Nginx**:
   ```
   nginx -t
   ```

4. **Prueba la API directamente**:
   ```
   curl -v -H "Origin: https://selfservice-dev.celero.network" https://api.celero.network/api/clientes/health
   ```

5. **Verifica los encabezados CORS**:
   ```
   curl -s -I -X OPTIONS -H "Origin: https://selfservice-dev.celero.network" -H "Access-Control-Request-Method: GET" https://api.celero.network/api/clientes/health
   ```

## Notas adicionales

- Si se realizan cambios en la API, asegúrate de que la configuración CORS en `Program.cs` incluya siempre todos los orígenes necesarios.
- La solución implementada cubre tanto el escenario donde la gestión de CORS está en la aplicación (método preferido) como una capa adicional en Nginx (respaldo).
- Los certificados SSL deben renovarse automáticamente si se utiliza Let's Encrypt, pero es importante verificar periódicamente que no hayan caducado.
