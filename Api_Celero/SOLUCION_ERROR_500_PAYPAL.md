# Solución Error 500 en PayPal Capture Payment

## Problema Identificado
El endpoint `/api/clientes/paypal/capture-payment` devuelve error 500 en producción al intentar capturar pagos de PayPal.

## Causas Encontradas

### 1. **Construcción incorrecta de URLs de PayPal**
- El método `GetPayPalAccessToken` no construía correctamente la URL cuando `BaseUrl` estaba configurado
- Concatenación incorrecta que podía resultar en URLs como: `https://api-m.sandbox.paypal.com//v1/oauth2/token`

### 2. **Configuración en modo Sandbox en producción**
- El archivo `appsettings.Production.json` está usando credenciales y URL de sandbox de PayPal
- Credenciales actuales:
  - ClientId: `ARNIspCAQv62W_je2OQmpHdFtYHAGJV1mRpCoGq29kn2MtA65Y9gqwrmv9OOGaHV56XcnVMzztf7hkyi`
  - Mode: `sandbox`
  - BaseUrl: `https://api-m.sandbox.paypal.com`

### 3. **Manejo de errores insuficiente**
- El método no proporcionaba suficiente información sobre los errores
- No distinguía entre diferentes tipos de errores (red, timeout, autenticación)
- Los logs no incluían suficiente contexto para depuración

## Soluciones Implementadas

### 1. **Mejorado el método GetPayPalAccessToken**
```csharp
// Construcción correcta de URLs
string tokenUrl;
if (!string.IsNullOrEmpty(_payPalSettings.BaseUrl))
{
    tokenUrl = _payPalSettings.BaseUrl.TrimEnd('/') + "/" + _payPalSettings.TokenEndpoint.TrimStart('/');
}
else
{
    _logger.LogWarning("PayPal BaseUrl no configurado, usando sandbox por defecto");
    tokenUrl = "https://api-m.sandbox.paypal.com/v1/oauth2/token";
}
```

### 2. **Mejorado el logging y manejo de errores**
- Agregado validación de configuración
- Logs más detallados con contexto completo
- Parseo de errores de PayPal para mensajes más descriptivos
- Manejo específico para diferentes tipos de excepciones:
  - `HttpRequestException`: Error de red (503)
  - `TaskCanceledException`: Timeout (504)
  - `JsonException`: Error de parseo (500)
  - Excepciones generales con más contexto

### 3. **Mejorado el método CapturePayPalPayment**
- Mejor validación de entrada
- Logs más detallados con OrderId, NumeroFactura y ClienteCode
- Respuestas de error más informativas con códigos de error específicos
- Parseo de errores de PayPal para proporcionar detalles específicos

## Acciones Requeridas

### 1. **URGENTE: Actualizar credenciales de PayPal para producción**
En `appsettings.Production.json`, cambiar:
```json
"PayPal": {
    "BaseUrl": "https://api-m.paypal.com",  // Cambiar de sandbox a live
    "Mode": "live",                          // Cambiar de sandbox a live
    "ClientId": "TU_CLIENT_ID_DE_PRODUCCION",
    "ClientSecret": "TU_CLIENT_SECRET_DE_PRODUCCION",
    "TokenEndpoint": "/v1/oauth2/token",
    "OrdersEndpoint": "/v2/checkout/orders",
    "CaptureEndpoint": "/v2/checkout/orders/{order_id}/capture"
}
```

### 2. **Desplegar los cambios**
```bash
# Compilar el proyecto
dotnet publish -c Release -o ./publish

# Reiniciar el servicio
sudo systemctl restart api-celero.service

# Verificar logs
sudo journalctl -u api-celero.service -f
```

### 3. **Verificar la conexión con PayPal**
Usar el endpoint de prueba:
```bash
curl -X GET https://tu-dominio.com/api/clientes/paypal/test-connection
```

### 4. **Monitorear los logs**
Los nuevos logs proporcionarán información detallada:
- URL exacta siendo utilizada
- Modo de PayPal (sandbox/live)
- Códigos de error específicos de PayPal
- Tiempos de respuesta

## Mejoras Adicionales Recomendadas

1. **Implementar variables de entorno para credenciales sensibles**
   ```bash
   export PAYPAL_CLIENT_ID="tu_client_id_produccion"
   export PAYPAL_CLIENT_SECRET="tu_secret_produccion"
   export PAYPAL_MODE="live"
   ```

2. **Implementar cache para tokens de PayPal**
   - Los tokens de PayPal tienen una duración de ~9 horas
   - Cachear el token reduciría latencia y llamadas a la API

3. **Implementar reintentos automáticos**
   - Para errores de red temporales
   - Con backoff exponencial

4. **Agregar métricas y alertas**
   - Tasa de éxito/fallo de pagos
   - Tiempos de respuesta de PayPal
   - Alertas para fallos repetidos

## Testing

### Prueba en Sandbox (actual configuración)
```javascript
// Frontend debe usar las credenciales de sandbox
const paypalConfig = {
    clientId: 'ARNIspCAQv62W_je2OQmpHdFtYHAGJV1mRpCoGq29kn2MtA65Y9gqwrmv9OOGaHV56XcnVMzztf7hkyi',
    environment: 'sandbox'
};
```

### Prueba en Producción (después de actualizar)
```javascript
// Frontend debe usar las credenciales de producción
const paypalConfig = {
    clientId: 'TU_CLIENT_ID_DE_PRODUCCION',
    environment: 'live'
};
```

## Resumen
El error 500 se debe principalmente a:
1. Uso de credenciales de sandbox en producción
2. Construcción incorrecta de URLs
3. Manejo de errores insuficiente

Las mejoras implementadas proporcionan mejor diagnóstico y manejo de errores. Sin embargo, **es crítico actualizar las credenciales de PayPal a las de producción** para resolver completamente el problema.