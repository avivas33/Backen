# Guía de Implementación - Sistema de Registro de Actividad

## Resumen

He implementado un sistema completo de registro de actividad para tu pasarela de pago con las siguientes características:

### Backend (.NET 9)

1. **Modelos de datos** (`Models/ActivityLog.cs`):
   - `ActivityLog`: Registro de eventos de actividad del usuario
   - `RequestLog`: Registro de todas las peticiones HTTP

2. **Base de datos SQLite**:
   - Contexto: `ActivityLogContext`
   - Archivo: `activity_logs.db`
   - Migraciones ya creadas

3. **Middleware global** (`Middleware/RequestLoggingMiddleware.cs`):
   - Captura automáticamente todas las peticiones
   - Registra IP, User-Agent, método, ruta, tiempo de respuesta
   - Maneja IPs detrás de proxies (X-Forwarded-For, etc.)

4. **API Controller** (`Controllers/ActivityLogController.cs`):
   - `POST /api/activitylog/track`: Registra eventos de actividad
   - `GET /api/activitylog/analytics/suspicious`: Analiza patrones sospechosos

### Frontend (React + TypeScript)

1. **Servicio de Fingerprinting** (`services/fingerprintService.ts`):
   - Genera fingerprint único del dispositivo
   - Captura: resolución, zona horaria, idioma, WebGL, Canvas, fuentes
   - Usa SHA-256 para generar hash único

2. **Hook de tracking** (`hooks/useActivityTracking.tsx`):
   - Hook reutilizable para cualquier componente
   - Métodos específicos para eventos de pago
   - Tracking automático de sesión

3. **Servicio de actividad** (`services/activityService.ts`):
   - Envío de eventos con batching inteligente
   - Eventos críticos se envían inmediatamente
   - Usa sendBeacon al cerrar ventana

4. **Componente ejemplo** (`components/PaymentExample.tsx`):
   - Muestra cómo implementar el tracking en un flujo de pago

## Instalación y configuración

### Backend

1. Aplicar las migraciones:
```bash
cd /home/avivas/Documentos/Api_Celero/Api_Celero
dotnet ef database update --context ActivityLogContext
```

2. Verificar que el middleware está registrado en `Program.cs` (ya está agregado)

3. La base de datos se creará automáticamente en `activity_logs.db`

### Frontend

1. Las dependencias ya están instaladas (`crypto-js`)

2. Importar y usar el hook en tus componentes:
```typescript
import { useActivityTracking } from '@/hooks/useActivityTracking';

const MyComponent = () => {
  const { trackPaymentAttempt } = useActivityTracking('client-123');
  
  // Usar en tu lógica de pago
  await trackPaymentAttempt('credit_card', 100.00, 'USD');
};
```

## Eventos disponibles

- `session_start`: Inicio de sesión
- `session_end`: Fin de sesión
- `page_view`: Vista de página
- `checkout_start`: Inicio del checkout
- `payment_attempt`: Intento de pago
- `payment_success`: Pago exitoso
- `payment_failed`: Pago fallido

## Consultas SQL

Ver archivo `SQL_QUERIES_ANALYSIS.md` para consultas predefinidas que detectan:
- Múltiples intentos fallidos desde una IP
- Fingerprints compartidos entre cuentas
- Accesos desde múltiples zonas horarias
- Cambios de ubicación imposibles
- Comportamiento tipo bot

## Seguridad y privacidad

1. **No se almacenan datos de tarjetas**
2. **IPs se capturan correctamente** incluso detrás de proxies
3. **Fingerprinting anónimo** - no usa PII
4. **Datos adicionales** se almacenan como JSON para flexibilidad

## Monitoreo

Accede al endpoint de análisis para ver actividad sospechosa:
```
GET /api/activitylog/analytics/suspicious?from=2024-01-01&to=2024-12-31
```

## Próximos pasos recomendados

1. Configurar alertas automáticas basadas en las consultas SQL
2. Implementar dashboard de visualización
3. Agregar políticas de retención de datos (GDPR)
4. Configurar rate limiting en el endpoint de tracking
5. Implementar webhooks para notificaciones en tiempo real