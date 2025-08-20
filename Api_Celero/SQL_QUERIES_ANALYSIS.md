# Consultas SQL para Análisis de Patrones Sospechosos

## 1. Múltiples intentos de pago fallidos desde la misma IP

```sql
-- Detectar IPs con múltiples intentos fallidos (más de 3 en las últimas 24 horas)
SELECT 
    IpAddress,
    COUNT(*) as FailedAttempts,
    COUNT(DISTINCT ClientId) as UniqueClients,
    MAX(CreatedAtUtc) as LastAttempt,
    GROUP_CONCAT(DISTINCT ClientId) as ClientIds
FROM ActivityLogs
WHERE 
    EventType = 'payment_failed' 
    AND CreatedAtUtc >= datetime('now', '-24 hours')
    AND IpAddress IS NOT NULL
GROUP BY IpAddress
HAVING COUNT(*) >= 3
ORDER BY FailedAttempts DESC;
```

## 2. Diferentes cuentas usando el mismo fingerprint

```sql
-- Detectar fingerprints compartidos entre múltiples clientes
SELECT 
    Fingerprint,
    COUNT(DISTINCT ClientId) as ClientCount,
    COUNT(*) as TotalEvents,
    GROUP_CONCAT(DISTINCT ClientId) as ClientIds,
    MIN(CreatedAtUtc) as FirstSeen,
    MAX(CreatedAtUtc) as LastSeen
FROM ActivityLogs
WHERE 
    Fingerprint IS NOT NULL 
    AND ClientId IS NOT NULL
GROUP BY Fingerprint
HAVING COUNT(DISTINCT ClientId) > 1
ORDER BY ClientCount DESC, TotalEvents DESC;
```

## 3. Clientes accediendo desde múltiples zonas horarias

```sql
-- Detectar clientes con accesos desde diferentes zonas horarias
WITH ClientTimeZones AS (
    SELECT 
        ClientId,
        TimeZone,
        COUNT(*) as AccessCount,
        MIN(CreatedAtUtc) as FirstAccess,
        MAX(CreatedAtUtc) as LastAccess
    FROM ActivityLogs
    WHERE 
        ClientId IS NOT NULL 
        AND TimeZone IS NOT NULL
    GROUP BY ClientId, TimeZone
)
SELECT 
    ClientId,
    COUNT(DISTINCT TimeZone) as TimeZoneCount,
    GROUP_CONCAT(DISTINCT TimeZone) as TimeZones,
    SUM(AccessCount) as TotalAccesses
FROM ClientTimeZones
GROUP BY ClientId
HAVING COUNT(DISTINCT TimeZone) > 1
ORDER BY TimeZoneCount DESC;
```

## 4. Análisis de velocidad de transacciones

```sql
-- Detectar múltiples intentos de pago en corto período de tiempo
WITH PaymentAttempts AS (
    SELECT 
        ClientId,
        IpAddress,
        CreatedAtUtc,
        LAG(CreatedAtUtc) OVER (PARTITION BY ClientId ORDER BY CreatedAtUtc) as PreviousAttempt
    FROM ActivityLogs
    WHERE EventType IN ('payment_attempt', 'payment_failed', 'payment_success')
)
SELECT 
    ClientId,
    IpAddress,
    CreatedAtUtc,
    PreviousAttempt,
    CAST((julianday(CreatedAtUtc) - julianday(PreviousAttempt)) * 24 * 60 AS INTEGER) as MinutesBetweenAttempts
FROM PaymentAttempts
WHERE 
    PreviousAttempt IS NOT NULL
    AND CAST((julianday(CreatedAtUtc) - julianday(PreviousAttempt)) * 24 * 60 AS INTEGER) < 5  -- Menos de 5 minutos
ORDER BY CreatedAtUtc DESC;
```

## 5. Análisis de cambios de dispositivo sospechosos

```sql
-- Detectar cambios frecuentes de fingerprint para el mismo cliente
WITH ClientFingerprints AS (
    SELECT 
        ClientId,
        Fingerprint,
        MIN(CreatedAtUtc) as FirstSeen,
        MAX(CreatedAtUtc) as LastSeen,
        COUNT(*) as UsageCount
    FROM ActivityLogs
    WHERE 
        ClientId IS NOT NULL 
        AND Fingerprint IS NOT NULL
    GROUP BY ClientId, Fingerprint
)
SELECT 
    ClientId,
    COUNT(DISTINCT Fingerprint) as DeviceCount,
    GROUP_CONCAT(Fingerprint || ' (' || UsageCount || ')') as Devices,
    SUM(UsageCount) as TotalEvents
FROM ClientFingerprints
GROUP BY ClientId
HAVING COUNT(DISTINCT Fingerprint) > 3  -- Más de 3 dispositivos diferentes
ORDER BY DeviceCount DESC;
```

## 6. Análisis de patrones geográficos imposibles

```sql
-- Detectar cambios de ubicación físicamente imposibles
WITH LocationChanges AS (
    SELECT 
        ClientId,
        IpAddress,
        TimeZone,
        CreatedAtUtc,
        LAG(IpAddress) OVER (PARTITION BY ClientId ORDER BY CreatedAtUtc) as PreviousIP,
        LAG(TimeZone) OVER (PARTITION BY ClientId ORDER BY CreatedAtUtc) as PreviousTimeZone,
        LAG(CreatedAtUtc) OVER (PARTITION BY ClientId ORDER BY CreatedAtUtc) as PreviousTime
    FROM ActivityLogs
    WHERE ClientId IS NOT NULL
)
SELECT 
    ClientId,
    CreatedAtUtc,
    PreviousTime,
    IpAddress,
    PreviousIP,
    TimeZone,
    PreviousTimeZone,
    CAST((julianday(CreatedAtUtc) - julianday(PreviousTime)) * 24 AS REAL) as HoursBetween
FROM LocationChanges
WHERE 
    PreviousTimeZone IS NOT NULL
    AND TimeZone != PreviousTimeZone
    AND CAST((julianday(CreatedAtUtc) - julianday(PreviousTime)) * 24 AS REAL) < 4  -- Menos de 4 horas
ORDER BY CreatedAtUtc DESC;
```

## 7. Dashboard de actividad sospechosa

```sql
-- Vista general de actividad sospechosa en las últimas 24 horas
SELECT 
    'Failed Payments' as MetricType,
    COUNT(DISTINCT IpAddress) as Value,
    'IPs with 3+ failed payments' as Description
FROM ActivityLogs
WHERE 
    EventType = 'payment_failed' 
    AND CreatedAtUtc >= datetime('now', '-24 hours')
GROUP BY IpAddress
HAVING COUNT(*) >= 3

UNION ALL

SELECT 
    'Shared Fingerprints' as MetricType,
    COUNT(*) as Value,
    'Fingerprints used by multiple clients' as Description
FROM (
    SELECT Fingerprint
    FROM ActivityLogs
    WHERE Fingerprint IS NOT NULL AND ClientId IS NOT NULL
    GROUP BY Fingerprint
    HAVING COUNT(DISTINCT ClientId) > 1
) as SharedFP

UNION ALL

SELECT 
    'Multiple TimeZones' as MetricType,
    COUNT(*) as Value,
    'Clients accessing from multiple timezones' as Description
FROM (
    SELECT ClientId
    FROM ActivityLogs
    WHERE ClientId IS NOT NULL AND TimeZone IS NOT NULL
    GROUP BY ClientId
    HAVING COUNT(DISTINCT TimeZone) > 1
) as MultiTZ

UNION ALL

SELECT 
    'Rapid Attempts' as MetricType,
    COUNT(*) as Value,
    'Payment attempts within 5 minutes' as Description
FROM (
    SELECT 
        ClientId,
        CreatedAtUtc,
        LAG(CreatedAtUtc) OVER (PARTITION BY ClientId ORDER BY CreatedAtUtc) as PreviousAttempt
    FROM ActivityLogs
    WHERE EventType IN ('payment_attempt', 'payment_failed')
) as Attempts
WHERE 
    PreviousAttempt IS NOT NULL
    AND CAST((julianday(CreatedAtUtc) - julianday(PreviousAttempt)) * 24 * 60 AS INTEGER) < 5;
```

## 8. Análisis de sesiones anómalas

```sql
-- Detectar sesiones con comportamiento anómalo
WITH SessionStats AS (
    SELECT 
        SessionId,
        ClientId,
        COUNT(DISTINCT EventType) as UniqueEventTypes,
        COUNT(*) as TotalEvents,
        MIN(CreatedAtUtc) as SessionStart,
        MAX(CreatedAtUtc) as SessionEnd,
        CAST((julianday(MAX(CreatedAtUtc)) - julianday(MIN(CreatedAtUtc))) * 24 * 60 AS INTEGER) as SessionDurationMinutes,
        COUNT(CASE WHEN EventType = 'payment_failed' THEN 1 END) as FailedPayments,
        COUNT(CASE WHEN EventType = 'payment_success' THEN 1 END) as SuccessfulPayments
    FROM ActivityLogs
    WHERE SessionId IS NOT NULL
    GROUP BY SessionId
)
SELECT 
    SessionId,
    ClientId,
    TotalEvents,
    SessionDurationMinutes,
    FailedPayments,
    SuccessfulPayments,
    CASE 
        WHEN FailedPayments > 3 THEN 'High Failed Attempts'
        WHEN SessionDurationMinutes < 1 AND TotalEvents > 10 THEN 'Bot-like Behavior'
        WHEN FailedPayments > 0 AND SuccessfulPayments > 0 THEN 'Mixed Results'
        ELSE 'Normal'
    END as SessionType
FROM SessionStats
WHERE 
    FailedPayments > 3 
    OR (SessionDurationMinutes < 1 AND TotalEvents > 10)
    OR (FailedPayments > 0 AND SuccessfulPayments > 0)
ORDER BY FailedPayments DESC, TotalEvents DESC;
```

## Notas de implementación

1. **Índices recomendados** para optimizar estas consultas:
   ```sql
   CREATE INDEX idx_activity_event_created ON ActivityLogs(EventType, CreatedAtUtc);
   CREATE INDEX idx_activity_ip ON ActivityLogs(IpAddress);
   CREATE INDEX idx_activity_fingerprint ON ActivityLogs(Fingerprint);
   CREATE INDEX idx_activity_client ON ActivityLogs(ClientId);
   CREATE INDEX idx_activity_session ON ActivityLogs(SessionId);
   ```

2. **Consideraciones de privacidad**:
   - Asegurar cumplimiento con GDPR/CCPA
   - Implementar políticas de retención de datos
   - Anonimizar datos después de cierto período

3. **Alertas automáticas**:
   - Configurar jobs que ejecuten estas consultas periódicamente
   - Enviar notificaciones cuando se detecten patrones sospechosos
   - Integrar con sistemas de monitoreo existentes