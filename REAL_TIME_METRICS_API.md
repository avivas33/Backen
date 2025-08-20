# API de Métricas en Tiempo Real

## Endpoints Disponibles

### 1. Dashboard Completo
```
GET /api/activitylog/dashboard?from={datetime}&to={datetime}
```

Retorna métricas completas del período especificado (por defecto últimas 24 horas).

**Respuesta:**
```json
{
  "generatedAt": "2024-01-20T10:30:00Z",
  "period": "2024-01-19 10:30 - 2024-01-20 10:30",
  "general": {
    "totalEvents": 15420,
    "uniqueClients": 234,
    "uniqueSessions": 456,
    "totalRequests": 25000,
    "averageResponseTime": 145.5
  },
  "security": {
    "suspiciousIPs": 5,
    "sharedFingerprints": 3,
    "multipleTimeZoneClients": 8,
    "rapidFireAttempts": 12,
    "failedPayments": 45,
    "threatScore": 35.5
  },
  "paymentStats": {
    "totalAttempts": 450,
    "successfulPayments": 380,
    "failedPayments": 70,
    "totalAmount": 125000.50,
    "successAmount": 98500.00,
    "successRate": 84.4,
    "paymentMethodBreakdown": {
      "credit_card": 300,
      "paypal": 100,
      "bank_transfer": 50
    },
    "errorCodeBreakdown": {
      "INSUFFICIENT_FUNDS": 40,
      "CARD_DECLINED": 20,
      "TIMEOUT": 10
    }
  },
  "hourlyActivity": [...],
  "topFailedIPs": [...],
  "topSharedFingerprints": [...],
  "recentSuspiciousActivity": [...]
}
```

### 2. Dashboard en Vivo
```
GET /api/activitylog/dashboard/live
```

Retorna métricas en tiempo real de los últimos 5 minutos con comparaciones.

**Respuesta:**
```json
{
  "metrics": [
    {
      "metricType": "events_per_hour",
      "label": "Events/Hour",
      "value": 1234,
      "previousValue": 1100,
      "changePercent": 12.18,
      "trend": "up",
      "timestamp": "2024-01-20T10:30:00Z"
    },
    {
      "metricType": "payment_success_rate",
      "label": "Payment Success Rate",
      "value": 85.5,
      "previousValue": 82.3,
      "changePercent": 3.89,
      "trend": "up"
    }
  ],
  "recentEvents": [...],
  "activeSessions": 45,
  "eventsPerMinute": 23,
  "lastUpdate": "2024-01-20T10:30:00Z"
}
```

### 3. Stream de Métricas (Server-Sent Events)
```
GET /api/activitylog/metrics/stream
```

Conexión persistente que envía actualizaciones cada 5 segundos.

**Ejemplo de uso en JavaScript:**
```javascript
const eventSource = new EventSource('/api/activitylog/metrics/stream');

eventSource.onmessage = (event) => {
  const data = JSON.parse(event.data);
  console.log('Métricas actualizadas:', data);
};

eventSource.onerror = (error) => {
  console.error('Error en stream:', error);
};

// Cerrar conexión cuando no se necesite
eventSource.close();
```

### 4. Actividad Sospechosa (Endpoint existente)
```
GET /api/activitylog/analytics/suspicious?from={datetime}&to={datetime}
```

## Threat Score

El sistema calcula automáticamente un "Threat Score" de 0-100 basado en:

- **IPs sospechosas** (3+ pagos fallidos): 5 puntos cada una (máx 30)
- **Fingerprints compartidos**: 10 puntos cada uno (máx 30)
- **Clientes multi-timezone**: 3 puntos cada uno (máx 20)
- **Intentos rápidos**: 2 puntos cada uno (máx 20)

### Interpretación del Threat Score:
- **0-30**: Riesgo bajo (verde)
- **31-70**: Riesgo medio (amarillo)
- **71-100**: Riesgo alto (rojo)

## Componente React de Dashboard

El componente `ActivityDashboard` incluye:

1. **Métricas en tiempo real** con indicadores de tendencia
2. **Métricas generales** (eventos, clientes, tasa de éxito)
3. **Panel de seguridad** con threat score
4. **Top IPs fallidas** con nivel de riesgo
5. **Actividad sospechosa reciente** con severidad

### Uso del componente:
```tsx
import { ActivityDashboard } from '@/components/ActivityDashboard';

function App() {
  return <ActivityDashboard />;
}
```

## Configuración de Alertas

Para recibir notificaciones automáticas, puedes configurar un servicio que consulte el endpoint cada X minutos:

```csharp
// Ejemplo de servicio de alertas
public class AlertService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var metrics = await _metricsService.GetDashboardMetricsAsync();
            
            if (metrics.Security.ThreatScore > 70)
            {
                // Enviar alerta crítica
                await SendCriticalAlert(metrics);
            }
            else if (metrics.Security.ThreatScore > 40)
            {
                // Enviar alerta de advertencia
                await SendWarningAlert(metrics);
            }
            
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

## Optimización de Rendimiento

1. Las consultas están optimizadas con índices apropiados
2. El servicio usa caché para métricas que no cambian frecuentemente
3. El streaming usa Server-Sent Events para eficiencia
4. Las métricas pesadas se calculan de forma asíncrona

## Seguridad

1. Los endpoints requieren autenticación (agregar según tu implementación)
2. Los datos sensibles (como fingerprints completos) se truncan en las respuestas
3. Se implementa rate limiting para prevenir abuso
4. Los logs de actividad se retienen según políticas de privacidad