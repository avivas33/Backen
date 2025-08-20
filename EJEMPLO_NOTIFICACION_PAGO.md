# 📧 Ejemplo de Notificación de Pago con Tarjeta de Crédito

## 🎯 Descripción
Este ejemplo muestra cómo funciona el sistema de notificación automática de pagos con tarjeta de crédito en tu API Celero.

## 🔧 Funcionamiento

### 1️⃣ Endpoint de Venta
```
POST /api/clientes/venta-tarjeta
```

### 2️⃣ Datos de la Solicitud (CobaltSaleRequest)
```json
{
  "amount": "50.00",
  "currency_code": "USD",
  "card_number": "4111111111111111",
  "expiry_month": "12",
  "expiry_year": "2025",
  "cvv": "123",
  "card_holder": "Juan Pérez",
  "customer_name": "Juan Pérez García",
  "customer_email": "juan.perez@email.com",
  "order_id": "ORD-2025-001",
  "description": "Servicio de Internet Premium - Plan Mensual"
}
```

### 3️⃣ Flujo de Procesamiento

1. **Procesamiento del Pago**: La API procesa el pago con Cobalt
2. **Extracción de Datos**: Se extraen los detalles de la transacción
3. **Envío de Notificaciones**: Se envían automáticamente 2 correos:
   - ✅ **Confirmación al Cliente** (si se proporciona `customer_email`)
   - 💰 **Notificación al Administrador**

## 📧 Ejemplos de Correos Enviados

### 📩 Correo de Confirmación al Cliente

**Asunto**: ✅ Confirmación de Pago - Celero Network

**Contenido HTML**:
```html
<!DOCTYPE html>
<html>
<body>
  <div style="background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; text-align: center;">
    <h1 style="color: white; margin: 0; font-size: 28px;">✅ Pago Confirmado</h1>
    <p style="color: #f8f9fa; margin: 10px 0 0 0; font-size: 16px;">¡Gracias por su compra!</p>
  </div>
  
  <div style="padding: 30px; background-color: #f8f9fa;">
    <div style="background-color: white; border-radius: 12px; padding: 25px;">
      <h2>Hola Juan Pérez García,</h2>
      <p>Su pago ha sido procesado exitosamente. A continuación encontrará los detalles:</p>
      
      <table style="width: 100%;">
        <tr>
          <td><strong>Orden:</strong></td>
          <td>ORD-2025-001</td>
        </tr>
        <tr>
          <td><strong>Descripción:</strong></td>
          <td>Servicio de Internet Premium - Plan Mensual</td>
        </tr>
        <tr>
          <td><strong>Fecha y Hora:</strong></td>
          <td>22/06/2025 14:30:25</td>
        </tr>
        <tr>
          <td><strong>Monto:</strong></td>
          <td style="color: #28a745; font-size: 20px; font-weight: bold;">$50.00 USD</td>
        </tr>
        <tr>
          <td><strong>Transacción:</strong></td>
          <td>TXN_ABC123456789</td>
        </tr>
        <tr>
          <td><strong>Estado:</strong></td>
          <td style="color: #28a745; font-weight: bold;">✅ APROBADO</td>
        </tr>
      </table>
    </div>
  </div>
</body>
</html>
```

### 📩 Correo de Notificación al Administrador

**Asunto**: 💰 Nueva Venta con Tarjeta de Crédito - Celero Network

**Destinatario**: admin@celero.net

**Contenido HTML**:
```html
<!DOCTYPE html>
<html>
<body>
  <div style="background: linear-gradient(135deg, #ff7b7b 0%, #667eea 100%); padding: 30px; text-align: center;">
    <h1 style="color: white; margin: 0; font-size: 28px;">💰 Nueva Venta Registrada</h1>
    <p style="color: #f8f9fa; margin: 10px 0 0 0; font-size: 16px;">Pago con Tarjeta de Crédito</p>
  </div>
  
  <div style="padding: 30px; background-color: #f8f9fa;">
    <div style="background-color: white; border-radius: 12px; padding: 25px;">
      <h2>Detalles de la Transacción</h2>
      
      <div style="background-color: #e3f2fd; border-left: 4px solid #2196f3; padding: 20px; margin: 20px 0;">
        <h3 style="color: #1976d2;">👤 Información del Cliente</h3>
        <table>
          <tr>
            <td><strong>Nombre:</strong></td>
            <td>Juan Pérez García</td>
          </tr>
          <tr>
            <td><strong>Email:</strong></td>
            <td>juan.perez@email.com</td>
          </tr>
        </table>
      </div>
      
      <div style="background-color: #f3e5f5; border-left: 4px solid #9c27b0; padding: 20px; margin: 20px 0;">
        <h3 style="color: #7b1fa2;">💳 Información del Pago</h3>
        <table>
          <tr>
            <td><strong>Orden:</strong></td>
            <td>ORD-2025-001</td>
          </tr>
          <tr>
            <td><strong>Descripción:</strong></td>
            <td>Servicio de Internet Premium - Plan Mensual</td>
          </tr>
          <tr>
            <td><strong>Monto:</strong></td>
            <td style="color: #e91e63; font-size: 22px; font-weight: bold;">$50.00 USD</td>
          </tr>
          <tr>
            <td><strong>Plataforma:</strong></td>
            <td>Cobalt Payment Gateway</td>
          </tr>
          <tr>
            <td><strong>Estado:</strong></td>
            <td style="color: #4caf50; font-weight: bold;">✅ PROCESADO EXITOSAMENTE</td>
          </tr>
        </table>
      </div>
    </div>
  </div>
</body>
</html>
```

## 🔧 Configuración Requerida

### Variables de Entorno (.env)
```env
# Configuración de Correo (Resend)
Resend__ApiKey=re_tu_api_key_aqui
Resend__DefaultFromEmail=avivas@celero.net
Resend__DefaultFromName=Celero Network

# Configuración de Cobalt
Cobalt__ClientId=tu_client_id
Cobalt__ClientSecret=tu_client_secret
```

### appsettings.json
```json
{
  "Resend": {
    "ApiKey": "re_af22v2eR_5UeSvpks4dfnJWTe6dLYJs5a",
    "DefaultFromEmail": "avivas@celero.net",
    "DefaultFromName": "Celero network"
  }
}
```

## 🚀 Ejemplo de Uso con cURL

```bash
curl -X POST "https://tu-api.com/api/clientes/venta-tarjeta" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": "50.00",
    "currency_code": "USD",
    "card_number": "4111111111111111",
    "expiry_month": "12",
    "expiry_year": "2025",
    "cvv": "123",
    "card_holder": "Juan Pérez",
    "customer_name": "Juan Pérez García",
    "customer_email": "juan.perez@email.com",
    "order_id": "ORD-2025-001",
    "description": "Servicio de Internet Premium - Plan Mensual"
  }'
```

## 📝 Respuesta de la API

### ✅ Respuesta Exitosa
```json
{
  "status": "success",
  "data": {
    "id": "TXN_ABC123456789",
    "authorization_number": "AUTH789456",
    "amount": "50.00",
    "currency": "USD",
    "status": "approved"
  }
}
```

### 📧 Logs de Notificaciones
```
[2025-06-22 14:30:26] INFO: Confirmación de pago enviada al cliente: juan.perez@email.com
[2025-06-22 14:30:27] INFO: Notificación de venta enviada al administrador
```

## ⚙️ Personalización

### Cambiar el Email del Administrador
En `ClientesController.cs`, línea ~540:
```csharp
var correoAdminEnviado = await EnviarCorreoInterno(
    "tu-admin@celero.net", // 👈 Cambiar aquí
    asuntoAdmin,
    cuerpoAdmin
);
```

### Agregar Campos Adicionales
Modifica `CobaltSaleRequest.cs` para incluir más datos:
```csharp
public class CobaltSaleRequest
{
    // ... campos existentes ...
    public string? customer_phone { get; set; }
    public string? billing_address { get; set; }
    public string? customer_id { get; set; }
}
```

## 🔒 Seguridad

- ✅ Las credenciales sensibles se mantienen en variables de entorno
- ✅ Los correos se envían de forma asíncrona sin bloquear la respuesta
- ✅ Los errores de correo no afectan el procesamiento del pago
- ✅ Se registran logs detallados para auditoría

## 🎯 Casos de Uso

1. **E-commerce**: Confirmaciones automáticas de compra
2. **SaaS**: Notificaciones de suscripciones pagadas
3. **Servicios**: Confirmaciones de pagos de facturas
4. **Administración**: Alertas instantáneas de ingresos

## 📊 Métricas de Seguimiento

El sistema registra:
- ✅ Pagos procesados exitosamente
- 📧 Correos enviados a clientes
- 🔔 Notificaciones enviadas a administradores
- ⚠️ Errores en el envío de correos (sin afectar pagos)
