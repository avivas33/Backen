# 🧪 Script de Prueba - Notificación de Pago

## Usar con Postman, Insomnia o cURL

### 📍 Endpoint
```
POST http://localhost:5000/api/clientes/venta-tarjeta
```

### 📋 Headers
```
Content-Type: application/json
Accept: application/json
```

### 📦 Body (JSON)
```json
{
  "amount": "25.99",
  "currency_code": "USD",
  "card_number": "4111111111111111",
  "expiry_month": "12",
  "expiry_year": "2025",
  "cvv": "123",
  "card_holder": "María González",
  "customer_name": "María González López",
  "customer_email": "maria.gonzalez@ejemplo.com",
  "order_id": "TEST-ORD-2025-001",
  "description": "Plan Premium Internet 50MB - Prueba de Notificación"
}
```

## 🎯 Qué Esperar

### 1️⃣ Respuesta del API
- ✅ Status 200 OK
- 🔄 Procesamiento del pago con Cobalt
- 📧 Envío automático de 2 correos

### 2️⃣ Correos Enviados

#### 📧 Al Cliente (maria.gonzalez@ejemplo.com)
- **Asunto**: ✅ Confirmación de Pago - Celero Network
- **Contenido**: Confirmación elegante con detalles de la transacción
- **Información incluida**:
  - Nombre del cliente
  - Monto pagado ($25.99 USD)
  - Orden (TEST-ORD-2025-001)
  - Descripción del servicio
  - Fecha y hora del pago
  - ID de transacción
  - Estado: APROBADO

#### 📧 Al Administrador (admin@celero.net)
- **Asunto**: 💰 Nueva Venta con Tarjeta de Crédito - Celero Network  
- **Contenido**: Resumen administrativo completo
- **Información incluida**:
  - Datos del cliente
  - Detalles de la transacción
  - Monto e información de pago
  - Plataforma utilizada (Cobalt)
  - Acción requerida

### 3️⃣ Logs en Consola
```
[INFO] Venta Cobalt exitosa
[INFO] Confirmación de pago enviada al cliente: maria.gonzalez@ejemplo.com
[INFO] Notificación de venta enviada al administrador
```

## 🔧 Ejemplo con cURL (Windows PowerShell)

```powershell
$body = @{
    amount = "25.99"
    currency_code = "USD"
    card_number = "4111111111111111"
    expiry_month = "12"
    expiry_year = "2025"
    cvv = "123"
    card_holder = "María González"
    customer_name = "María González López"
    customer_email = "maria.gonzalez@ejemplo.com"
    order_id = "TEST-ORD-2025-001"  
    description = "Plan Premium Internet 50MB - Prueba de Notificación"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/clientes/venta-tarjeta" -Method Post -Body $body -ContentType "application/json"
```

## 🔧 Ejemplo con cURL (bash/Linux)

```bash
curl -X POST "http://localhost:5000/api/clientes/venta-tarjeta" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -d '{
    "amount": "25.99",
    "currency_code": "USD", 
    "card_number": "4111111111111111",
    "expiry_month": "12",
    "expiry_year": "2025",
    "cvv": "123",
    "card_holder": "María González",
    "customer_name": "María González López", 
    "customer_email": "maria.gonzalez@ejemplo.com",
    "order_id": "TEST-ORD-2025-001",
    "description": "Plan Premium Internet 50MB - Prueba de Notificación"
  }'
```

## ⚠️ Importante para Pruebas

### 🔧 Configuración Previa
1. ✅ Asegúrate de que las variables de entorno estén configuradas
2. ✅ Verifica que el servicio Resend esté funcionando
3. ✅ Confirma que las credenciales de Cobalt sean válidas

### 📧 Email de Prueba  
- Usa un email real que puedas revisar
- El email del administrador está configurado como `admin@celero.net`
- Puedes cambiar este email en el código si necesitas

### 🧪 Datos de Tarjeta de Prueba
La tarjeta `4111111111111111` es una tarjeta de prueba de Visa que funciona en entornos de testing.

## 🎭 Escenarios de Prueba

### ✅ Prueba Exitosa (con email de cliente)
```json
{
  "customer_email": "tu-email@ejemplo.com"
}
```
**Resultado**: 2 correos enviados (cliente + admin)

### ✅ Prueba Sin Email de Cliente
```json
{
  "customer_email": null
}
```
**Resultado**: 1 correo enviado (solo admin)

### ⚠️ Prueba con Email Inválido
```json
{
  "customer_email": "email-invalido"
}
```
**Resultado**: Error en envío al cliente, pero admin recibe notificación

## 🔍 Verificación

### En los Logs
Busca estas líneas para confirmar el funcionamiento:
```
✅ "Venta Cobalt exitosa"
✅ "Confirmación de pago enviada al cliente: {email}"  
✅ "Notificación de venta enviada al administrador"
```

### En tu Bandeja de Entrada
- 📧 Revisa tu email para ver la confirmación del cliente
- 📧 Revisa admin@celero.net para la notificación administrativa

¡Con este ejemplo tienes todo lo necesario para probar las notificaciones de pago con tarjeta de crédito! 🚀
