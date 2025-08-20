# Documentación - Sistema de Notificación de Recibos desde Hansa

## Descripción General
Se ha implementado un sistema completo para consultar recibos desde la API de Hansa (IPVc) y enviar notificaciones por correo electrónico a los clientes con el detalle de su pago. 

**ACTUALIZACIÓN IMPORTANTE:** El sistema ahora envía automáticamente la notificación por correo cuando se registra exitosamente cualquier recibo en Hansa (PayPal, Tarjetas de Crédito, Yappy, etc.).

## Archivos Creados

### 1. Modelos
- **`/Api_Celero/Models/HansaReceiptModels.cs`**
  - Modelos para manejar la respuesta de la API de Hansa
  - Modelos para la solicitud de envío de correo
  - Mapeo de campos según la estructura del JSON de Hansa

### 2. Servicios
- **`/Api_Celero/Services/HansaReceiptService.cs`**
  - Servicio para consultar la API de Hansa
  - Manejo de autenticación básica
  - Control de timeout y errores

### 3. Controlador
- **`/Api_Celero/Controllers/RecibosController.cs`**
  - Endpoints para consultar y enviar notificaciones de recibos
  - Procesamiento de datos y formateo

### 4. Modificaciones
- **`/Api_Celero/Services/EmailService.cs`**
  - Agregado método `SendReceiptEmailAsync`
  - Plantilla HTML respetando el diseño corporativo exacto
  - Versión de texto plano para compatibilidad

- **`/Api_Celero/Program.cs`**
  - Registrado el servicio `IHansaReceiptService`

- **`/Api_Celero/Controllers/ClientesController.cs`**
  - Inyectado `IHansaReceiptService` en el constructor
  - Creado método reutilizable `EnviarNotificacionReciboAsync()` para todos los tipos de pago
  - **Excluye automáticamente ACH** del envío de correos
  - Integrado en el endpoint `/recibos` (usado por todos los métodos de pago)
  - Integrado en el método `CapturePayPalPayment` para PayPal
  - Automáticamente detecta el tipo de pago y envía notificación

- **`/Api_Celero/Models/ReciboRequest.cs`**
  - Agregado campo opcional `Email` para especificar email del cliente
  - Cambiado tipo de `Sum` de string a decimal para mejor manejo

## Cambios en Frontend

- **`/selfservice_Celero/src/services/pagoService.ts`**
  - Agregado campo opcional `email` a la interfaz `ReciboData`

- **`/selfservice_Celero/src/pages/InvoiceDetails.tsx`**
  - Agregado `email: currentState.eMail` en creación de recibos (PayPal, Tarjeta, Yappy)
  - **ACH excluido** del campo email (no necesario ya que se excluye automáticamente)
  - Corregido uso de `pagoService.crearRecibo` para Yappy (era `clienteService.registrarPagoACH`)
  - Ajustado tipo de datos en `sum` para consistencia con el backend

## Endpoints Disponibles

### 1. Enviar Notificación de Recibo
```http
POST /api/recibos/enviar-notificacion
Content-Type: application/json

{
  "receiptNumber": "1030003",
  "companyCode": "2",
  "customerEmail": "cliente@example.com"
}
```

**Respuesta exitosa:**
```json
{
  "success": true,
  "message": "Notificación enviada exitosamente",
  "data": {
    "receiptNumber": "1030003",
    "customerEmail": "cliente@example.com",
    "customerName": "ADIL SALIH",
    "totalAmount": 89.52,
    "transactionDate": "13 de agosto, 2025",
    "paymentMethod": "PAYPAL",
    "detailsCount": 3,
    "emailId": "msg_xxx"
  }
}
```

### 2. Consultar Recibo (Solo para pruebas)
```http
GET /api/recibos/consultar/{receiptNumber}?companyCode=2
```

## Mapeo de Campos

### Campos de la API de Hansa → Plantilla de Email

| Campo Hansa | Campo en Plantilla | Descripción |
|-------------|-------------------|-------------|
| `InvoiceNr` | REFERENCIA | Número de factura |
| `InvoiceOfficialSerNr` | Nro. CUFE | Número oficial de factura |
| `RecVal` | RECIBIDO | Monto recibido por factura |
| `CurPayVal` | Total aplicado | Monto total del recibo |
| `CustName` | Estimado | Nombre del cliente |
| `TransDate` | Fecha de recibo | Fecha de la transacción |
| `PayMode` | Método de pago | Forma de pago utilizada |
| - | CUOTA | Siempre "0" (según especificación) |

## Configuración de Logos por Empresa

- **Empresa 2 (Celero Networks):** `https://i.imgur.com/DYW7gJG.png`
- **Empresa 3 (Celero Quantum):** `https://i.imgur.com/QcHzleK.png`

El logo se selecciona automáticamente según el `companyCode` enviado en la petición.

## Características Implementadas

### 1. Consulta a Hansa
- URL construida dinámicamente con el número de recibo
- Autenticación básica usando credenciales del `appsettings.json`
- Timeout configurable (30 segundos por defecto)
- Manejo de errores HTTP y timeouts

### 2. Procesamiento de Datos
- Parseo seguro de montos decimales
- Formateo de fechas en español
- Mapeo de métodos de pago a nombres amigables
- Validación de datos antes del envío

### 3. Envío de Email
- Plantilla HTML idéntica a la proporcionada
- Versión de texto plano para compatibilidad
- Headers optimizados para evitar spam
- Tags para tracking de emails

### 4. Manejo de Errores
- Validación de parámetros requeridos
- Mensajes de error descriptivos
- Logging detallado para debugging
- Códigos HTTP apropiados

## Configuración Requerida en appsettings.json

```json
{
  "Hansa": {
    "BaseUrl": "http://10.1.8.3",
    "WebPort": 8015,
    "Usuario": "av@celero.net",
    "Clave": "Panamaitlab2025",
    "UseBasicAuth": true,
    "TimeoutSeconds": 30
  },
  "Resend": {
    "ApiKey": "re_xxx",
    "DefaultFromEmail": "noreply@celero.net",
    "DefaultFromName": "Celero Network"
  }
}
```

## Flujo del Proceso

### Flujo Manual (Endpoint directo)
1. **Cliente llama al endpoint** con número de recibo, código de empresa y email
2. **El servicio consulta Hansa** usando la API IPVc con autenticación básica
3. **Se procesa la respuesta JSON** extrayendo los datos relevantes
4. **Se formatea la información** (fechas en español, montos con formato monetario)
5. **Se genera el email** con la plantilla corporativa exacta
6. **Se envía el correo** usando Resend con headers optimizados
7. **Se retorna confirmación** con detalles del envío

### Flujo Automático (Todos los Métodos de Pago)
1. **Usuario completa pago** (PayPal, Tarjeta de Crédito, Yappy)
2. **Se procesa el pago** según el método correspondiente
3. **Se registra el recibo en Hansa** usando el endpoint `/recibos` o directamente
4. **Si el registro es exitoso** (serNrRegistrado tiene valor):
   - Se obtiene el email del cliente (desde el request, PayPal, o base de datos)
   - Se consulta Hansa para obtener detalles completos del recibo registrado
   - Se envía automáticamente la notificación por correo con plantilla corporativa
   - Se registra en logs el resultado del envío
5. **El proceso continúa** sin fallar aunque el email no se envíe

### Métodos de Pago Soportados:
- **PayPal:** ✅ Email obtenido de la respuesta de PayPal, registra recibo directamente
- **Tarjetas de Crédito:** ✅ Email obtenido del estado del cliente, registra vía `/recibos`
- **Yappy:** ✅ Email obtenido del estado del cliente, registra vía `/recibos`
- **ACH:** ❌ **NO envía correo automático** (excluido por configuración), solo registra recibo
- **Otros:** ✅ Email obtenido del campo opcional en ReciboRequest

## Consideraciones de Seguridad

- Autenticación básica para Hansa (credenciales en configuración)
- Validación de entrada en todos los endpoints
- Sin exposición de información sensible en logs
- Headers de email configurados para mejorar entregabilidad

## Pruebas Recomendadas

1. **Probar consulta directa:**
   ```bash
   curl -X GET "http://localhost:7262/api/recibos/consultar/1030003?companyCode=2"
   ```

2. **Probar envío de notificación:**
   ```bash
   curl -X POST "http://localhost:7262/api/recibos/enviar-notificacion" \
     -H "Content-Type: application/json" \
     -d '{
       "receiptNumber": "1030003",
       "companyCode": "2",
       "customerEmail": "test@example.com"
     }'
   ```

## Notas Importantes

- El campo CUOTA siempre se muestra como "0" según lo especificado
- La plantilla de email NO debe ser modificada (diseño corporativo estricto)
- Los logos cambian automáticamente según la empresa
- El método de pago se mapea a nombres amigables (PAYPAL, ACH, TARJETA DE CRÉDITO, etc.)
- Las fechas se formatean en español (ej: "13 de agosto, 2025")

## Solución de Problemas

### Error 504 (Timeout)
- Verificar conectividad con servidor Hansa (10.1.8.3:8015)
- Aumentar `TimeoutSeconds` en configuración si es necesario

### Error 502 (Bad Gateway)
- Verificar credenciales de Hansa en configuración
- Confirmar que el servidor Hansa está activo

### Email no llega
- Verificar API Key de Resend
- Revisar logs para ver el `emailId` retornado
- Confirmar que el email del destinatario es válido

## Mantenimiento

Para agregar nuevos campos o modificar el mapeo:
1. Actualizar `HansaReceiptModels.cs` con los nuevos campos
2. Modificar el procesamiento en `RecibosController.cs`
3. Ajustar la plantilla en `EmailService.cs` método `GenerateReceiptEmailHtml`