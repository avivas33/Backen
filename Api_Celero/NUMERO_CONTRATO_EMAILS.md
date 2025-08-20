# Agregado Campo "Número de Contrato" a Notificaciones de Pago

## Resumen de Cambios

Se ha agregado el campo **"Número de Contrato"** a las notificaciones de pago por correo electrónico tanto para clientes como para administradores.

## Archivos Modificados

### 1. `Models/CobaltSaleRequest.cs`
- **Agregado**: Campo `contract_number` como propiedad opcional
- **Propósito**: Permitir que el frontend envíe un número de contrato específico
- **Tipo**: `string?` (opcional)

```csharp
// Nuevo campo agregado
public string? contract_number { get; set; }
```

### 2. `Controllers/ClientesController.cs`

#### Métodos de Generación de Templates Actualizados:
- **`GenerarCorreoCliente`**: Agregado parámetro `numeroContrato`
- **`GenerarCorreoAdministrador`**: Agregado parámetro `numeroContrato`

#### Lógica de Asignación:
```csharp
// Si se proporciona contract_number lo usa, sino usa order_id como fallback
var numeroContrato = !string.IsNullOrEmpty(venta.contract_number) ? venta.contract_number : venta.order_id ?? ordenId;
```

## Templates de Email Actualizados

### Template del Cliente
Ahora incluye en la sección de detalles de transacción:
```html
<tr>
    <td style='padding: 8px 0; font-weight: bold; color: #555; vertical-align: top;'>Número de Contrato:</td>
    <td style='padding: 8px 0; color: #333; font-family: monospace; font-size: 14px; vertical-align: top;'>{numeroContrato}</td>
</tr>
```

### Template del Administrador
Incluye la misma información en la sección de "💳 Información del Pago":
```html
<tr>
    <td style='padding: 8px 0; font-weight: bold; color: #555; vertical-align: top;'>Número de Contrato:</td>
    <td style='padding: 8px 0; color: #333; font-family: monospace; font-size: 14px; vertical-align: top;'>{numeroContrato}</td>
</tr>
```

## Posición en el Email

El campo "Número de Contrato" aparece en el siguiente orden:

1. ✅ **Orden** (Order ID)
2. 🆕 **Número de Contrato** (Nuevo campo)
3. 📝 **Descripción**
4. 📅 **Fecha y Hora**
5. 💰 **Monto**
6. 🔢 **Transacción**
7. 🔐 **Autorización** (si está disponible)
8. ✅ **Estado**

## Formato Visual

- **Etiqueta**: "Número de Contrato:" en negrita
- **Fuente**: Monospace para mejor legibilidad de códigos
- **Tamaño**: 14px
- **Color**: #333 (gris oscuro)
- **Estilo**: Misma línea que otros campos de datos

## Comportamiento

### Cuando se proporciona `contract_number`:
```json
{
  "order_id": "ORD-2025-001",
  "contract_number": "CONT-2025-ABC123",
  ...
}
```
**Resultado**: Se muestra "CONT-2025-ABC123" como número de contrato

### Cuando NO se proporciona `contract_number`:
```json
{
  "order_id": "ORD-2025-001",
  ...
}
```
**Resultado**: Se muestra "ORD-2025-001" como número de contrato (fallback)

## Uso desde el Frontend

Para incluir un número de contrato específico, el frontend debe enviar:

```javascript
// POST /api/Clientes/venta-tarjeta
{
  "currency_code": "USD",
  "amount": "100.00",
  "pan": "4111111111111111",
  "exp_date": "1225",
  "customer_email": "cliente@ejemplo.com",
  "customer_name": "Juan Pérez",
  "order_id": "ORD-2025-001",
  "contract_number": "CONT-2025-ABC123", // ← Nuevo campo opcional
  "description": "Pago de servicio"
}
```

## Compatibilidad

- ✅ **Backward compatible**: El campo es opcional
- ✅ **Fallback inteligente**: Usa `order_id` si no se proporciona `contract_number`
- ✅ **No rompe implementaciones existentes**: Las llamadas sin el campo funcionan normalmente

## Testing

Para probar la funcionalidad:

1. **Con número de contrato**: Enviar request con `contract_number`
2. **Sin número de contrato**: Enviar request sin `contract_number` (debe usar `order_id`)
3. **Verificar emails**: Confirmar que aparece el campo en ambos templates (cliente y admin)

Los cambios están implementados y listos para uso inmediato.
