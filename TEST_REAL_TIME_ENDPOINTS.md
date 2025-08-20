# Pruebas de Endpoints de Métricas en Tiempo Real

## Endpoints implementados

### 1. Dashboard completo
```bash
curl -X GET "http://localhost:7262/api/activitylog/dashboard" \
  -H "Content-Type: application/json"
```

### 2. Dashboard en vivo
```bash
curl -X GET "http://localhost:7262/api/activitylog/dashboard/live" \
  -H "Content-Type: application/json"
```

### 3. Stream de métricas (Server-Sent Events)
```bash
curl -X GET "http://localhost:7262/api/activitylog/metrics/stream" \
  -H "Accept: text/event-stream"
```

### 4. Registrar actividad (para generar datos de prueba)
```bash
curl -X POST "http://localhost:7262/api/activitylog/track" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "payment_attempt",
    "fingerprint": "abc123def456",
    "timeZone": "America/Panama",
    "screenResolution": "1920x1080",
    "browserLanguage": "es",
    "clientId": "client-test-001",
    "sessionId": "session-123",
    "paymentMethod": "credit_card",
    "amount": 100.50,
    "currency": "USD"
  }'
```

## Script de prueba completo

Crear archivo `test_endpoints.sh`:

```bash
#!/bin/bash

echo "=== Probando sistema de métricas en tiempo real ==="

# Base URL
BASE_URL="http://localhost:7262/api/activitylog"

echo "1. Generando datos de prueba..."

# Generar algunos eventos de prueba
for i in {1..5}; do
  curl -s -X POST "$BASE_URL/track" \
    -H "Content-Type: application/json" \
    -d "{
      \"eventType\": \"payment_attempt\",
      \"fingerprint\": \"fingerprint-$i\",
      \"timeZone\": \"America/Panama\",
      \"screenResolution\": \"1920x1080\",
      \"browserLanguage\": \"es\",
      \"clientId\": \"client-$i\",
      \"sessionId\": \"session-$i\",
      \"paymentMethod\": \"credit_card\",
      \"amount\": $((100 + i * 10)),
      \"currency\": \"USD\"
    }" > /dev/null
  echo "Evento $i enviado"
  sleep 1
done

# Generar algunos pagos exitosos
for i in {1..3}; do
  curl -s -X POST "$BASE_URL/track" \
    -H "Content-Type: application/json" \
    -d "{
      \"eventType\": \"payment_success\",
      \"fingerprint\": \"fingerprint-$i\",
      \"timeZone\": \"America/Panama\",
      \"screenResolution\": \"1920x1080\",
      \"browserLanguage\": \"es\",
      \"clientId\": \"client-$i\",
      \"sessionId\": \"session-$i\",
      \"paymentMethod\": \"credit_card\",
      \"amount\": $((100 + i * 10)),
      \"currency\": \"USD\",
      \"paymentStatus\": \"completed\"
    }" > /dev/null
  echo "Pago exitoso $i enviado"
  sleep 1
done

# Generar algunos pagos fallidos
for i in {4..5}; do
  curl -s -X POST "$BASE_URL/track" \
    -H "Content-Type: application/json" \
    -d "{
      \"eventType\": \"payment_failed\",
      \"fingerprint\": \"fingerprint-$i\",
      \"timeZone\": \"America/Panama\",
      \"screenResolution\": \"1920x1080\",
      \"browserLanguage\": \"es\",
      \"clientId\": \"client-$i\",
      \"sessionId\": \"session-$i\",
      \"paymentMethod\": \"credit_card\",
      \"amount\": $((100 + i * 10)),
      \"currency\": \"USD\",
      \"paymentStatus\": \"failed\",
      \"errorCode\": \"INSUFFICIENT_FUNDS\"
    }" > /dev/null
  echo "Pago fallido $i enviado"
  sleep 1
done

echo ""
echo "2. Probando dashboard completo..."
curl -s "$BASE_URL/dashboard" | jq '.'

echo ""
echo "3. Probando dashboard en vivo..."
curl -s "$BASE_URL/dashboard/live" | jq '.'

echo ""
echo "4. Probando actividad sospechosa..."
curl -s "$BASE_URL/analytics/suspicious" | jq '.'

echo ""
echo "5. Para probar el stream de métricas, ejecuta en otra terminal:"
echo "curl -X GET \"$BASE_URL/metrics/stream\" -H \"Accept: text/event-stream\""

echo ""
echo "=== Pruebas completadas ==="
```

## Ejecución de las pruebas

1. Hacer el script ejecutable:
```bash
chmod +x test_endpoints.sh
```

2. Iniciar el servidor:
```bash
cd /home/avivas/Documentos/Api_Celero/Api_Celero
dotnet run
```

3. En otra terminal, ejecutar las pruebas:
```bash
./test_endpoints.sh
```

## Ejemplo de respuesta del Dashboard

```json
{
  "generatedAt": "2024-01-20T15:30:00.000Z",
  "period": "2024-01-19 15:30 - 2024-01-20 15:30",
  "general": {
    "totalEvents": 10,
    "uniqueClients": 5,
    "uniqueSessions": 5,
    "totalRequests": 15,
    "averageResponseTime": 120.5
  },
  "security": {
    "suspiciousIPs": 0,
    "sharedFingerprints": 0,
    "multipleTimeZoneClients": 0,
    "rapidFireAttempts": 0,
    "failedPayments": 2,
    "threatScore": 8.0
  },
  "paymentStats": {
    "totalAttempts": 5,
    "successfulPayments": 3,
    "failedPayments": 2,
    "totalAmount": 750.0,
    "successAmount": 330.0,
    "successRate": 60.0,
    "paymentMethodBreakdown": {
      "credit_card": 5
    },
    "errorCodeBreakdown": {
      "INSUFFICIENT_FUNDS": 2
    }
  }
}
```

## Integración con el Frontend

Para usar el componente React del dashboard:

```tsx
// En tu App.tsx o donde quieras mostrar el dashboard
import { ActivityDashboard } from './components/ActivityDashboard';

function App() {
  return (
    <div className="App">
      <ActivityDashboard />
    </div>
  );
}
```

El componente se conectará automáticamente a los endpoints y mostrará las métricas en tiempo real.