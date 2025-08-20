# Integración de PayPal en API Celero

## Descripción
Se ha agregado soporte completo para pagos con PayPal al sistema API Celero. La integración incluye dos endpoints principales para crear órdenes y capturar pagos, siguiendo el flujo estándar de PayPal.

## Archivos Modificados

### 1. Modelos
- **`C:\Users\avivas\Desktop\Ubunto\Api_Celero\Api_Celero\Models\PayPalModels.cs`** (NUEVO)
  - Contiene todos los modelos necesarios para la integración con PayPal
  - Incluye modelos para solicitudes, respuestas y estructuras internas de PayPal

### 2. Configuración
- **`C:\Users\avivas\Desktop\Ubunto\Api_Celero\Api_Celero\Models\PaymentSettings.cs`**
  - Agregada clase `PayPalSettings` con configuración para PayPal
  
- **`C:\Users\avivas\Desktop\Ubunto\Api_Celero\Api_Celero\Models\HansaSettings.cs`**
  - Agregado `PayPal` a `PaymentMethodsConfig`

- **`C:\Users\avivas\Desktop\Ubunto\Api_Celero\Api_Celero\Program.cs`**
  - Registrado `PayPalSettings` en el contenedor de dependencias
  - Soporte para variables de entorno: `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_MODE`

- **`C:\Users\avivas\Desktop\Ubunto\Api_Celero\Api_Celero\appsettings.json`**
  - Agregada configuración de PayPal
  - Habilitado PayPal para todas las empresas

### 3. Controlador
- **`C:\Users\avivas\Desktop\Ubunto\Api_Celero\Api_Celero\Controllers\ClientesController.cs`**
  - Agregados 2 nuevos endpoints para PayPal
  - Métodos auxiliares para autenticación y generación de emails
  - Actualizados endpoints de payment-methods para incluir PayPal

## Endpoints Agregados

### 1. Crear Orden de PayPal
```
POST /api/clientes/paypal/create-order
```

**Request Body:**
```json
{
  "clienteCode": "CU-12345",
  "numeroFactura": "F001-000123",
  "amount": 100.50,
  "currency": "USD",
  "description": "Pago de factura F001-000123",
  "returnUrl": "https://tu-sitio.com/success",
  "cancelUrl": "https://tu-sitio.com/cancel",
  "emailCliente": "cliente@ejemplo.com",
  "nombreCliente": "Juan Pérez"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Orden de pago creada exitosamente",
  "data": {
    "orderId": "5O190127TN364715T",
    "approvalUrl": "https://www.sandbox.paypal.com/checkoutnow?token=5O190127TN364715T",
    "status": "CREATED"
  }
}
```

### 2. Capturar Pago de PayPal
```
POST /api/clientes/paypal/capture-payment
```

**Request Body:**
```json
{
  "orderId": "5O190127TN364715T",
  "clienteCode": "CU-12345",
  "numeroFactura": "F001-000123"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Pago capturado exitosamente",
  "data": {
    "transactionId": "8MC585209K746392H",
    "orderId": "5O190127TN364715T",
    "status": "COMPLETED",
    "amount": "100.50",
    "currency": "USD",
    "payer": {
      "name": "Juan Pérez",
      "email": "cliente@ejemplo.com",
      "payerId": "BXCVB7774A9QQ"
    },
    "captureTime": "2023-01-01T12:00:00Z",
    "numeroFactura": "F001-000123"
  }
}
```

### 3. Endpoints Actualizados
Los siguientes endpoints ahora incluyen información de PayPal:
- `GET /api/clientes/payment-methods/{empresaCode}`
- `GET /api/clientes/payment-methods`

## Configuración Requerida

### Variables de Entorno (Recomendado)
```bash
PAYPAL_CLIENT_ID=tu_client_id_de_paypal
PAYPAL_CLIENT_SECRET=tu_client_secret_de_paypal
PAYPAL_MODE=sandbox  # o "live" para producción
```

### Configuración en appsettings.json
```json
{
  "PayPal": {
    "BaseUrl": "https://api-m.sandbox.paypal.com",
    "Mode": "sandbox",
    "ClientId": "YOUR_PAYPAL_CLIENT_ID",
    "ClientSecret": "YOUR_PAYPAL_CLIENT_SECRET",
    "TokenEndpoint": "/v1/oauth2/token",
    "OrdersEndpoint": "/v2/checkout/orders",
    "CaptureEndpoint": "/v2/checkout/orders/{order_id}/capture"
  }
}
```

**Para Producción:**
```json
{
  "PayPal": {
    "BaseUrl": "https://api-m.paypal.com",
    "Mode": "live"
  }
}
```

## Flujo de Pago

1. **Crear Orden**: El cliente hace POST a `/paypal/create-order`
2. **Redirección**: Se redirige al usuario a `approvalUrl` de PayPal
3. **Aprobación**: El usuario aprueba el pago en PayPal
4. **Retorno**: PayPal redirige a `returnUrl` con el `token` (order ID)
5. **Captura**: Tu aplicación hace POST a `/paypal/capture-payment`
6. **Confirmación**: Se envían emails de confirmación automáticamente

## Características

- ✅ Autenticación automática con PayPal
- ✅ Manejo de errores completo
- ✅ Logging detallado
- ✅ Envío automático de emails de confirmación
- ✅ Soporte para múltiples monedas
- ✅ Configuración por empresa
- ✅ Variables de entorno para seguridad
- ✅ Soporte para sandbox y producción

## Notas de Seguridad

1. **Nunca** hardcodear credenciales en el código
2. Usar variables de entorno para `CLIENT_ID` y `CLIENT_SECRET`
3. Validar siempre el estado del pago antes de procesar
4. Implementar logs de auditoría para todas las transacciones
5. Usar HTTPS en todos los endpoints de producción

## Testing

Para probar en sandbox:
1. Crear cuenta de developer en PayPal
2. Obtener credenciales de sandbox
3. Configurar variables de entorno
4. Usar cuentas de prueba de PayPal para testing

## Próximos Pasos

1. Configurar webhooks de PayPal para notificaciones en tiempo real
2. Implementar reembolsos automáticos
3. Agregar soporte para pagos recurrentes
4. Implementar dashboard de transacciones PayPal