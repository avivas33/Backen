using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Api_Celero.Models;

namespace Api_Celero.Services
{
    public interface IMetricsService
    {
        Task<DashboardMetrics> GetDashboardMetricsAsync(DateTime? from = null, DateTime? to = null);
        Task<LiveDashboard> GetLiveMetricsAsync();
        Task<object> GetSuspiciousActivitiesAsync(int hours = 24);
    }

    public class MetricsService : IMetricsService
    {
        private readonly ActivityLogContext _context;
        private readonly ILogger<MetricsService> _logger;
        private static readonly Dictionary<string, RealTimeMetric> _cachedMetrics = new();
        private static DateTime _lastMetricUpdate = DateTime.UtcNow;

        public MetricsService(ActivityLogContext context, ILogger<MetricsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<DashboardMetrics> GetDashboardMetricsAsync(DateTime? from = null, DateTime? to = null)
        {
            var fromDate = from ?? DateTime.UtcNow.AddHours(-24);
            var toDate = to ?? DateTime.UtcNow;

            var dashboard = new DashboardMetrics
            {
                GeneratedAt = DateTime.UtcNow,
                Period = $"{fromDate:yyyy-MM-dd HH:mm} - {toDate:yyyy-MM-dd HH:mm}"
            };

            // Obtener métricas generales
            dashboard.General = await GetGeneralMetricsAsync(fromDate, toDate);
            
            // Obtener métricas de seguridad
            dashboard.Security = await GetSecurityMetricsAsync(fromDate, toDate);
            
            // Obtener actividad por hora
            dashboard.HourlyActivity = await GetHourlyActivityAsync(fromDate, toDate);
            
            // Obtener top IPs fallidas
            dashboard.TopFailedIPs = await GetTopFailedIPsAsync(fromDate, toDate);
            
            // Obtener fingerprints compartidos
            dashboard.TopSharedFingerprints = await GetTopSharedFingerprintsAsync(fromDate, toDate);
            
            // Obtener actividad sospechosa reciente
            dashboard.RecentSuspiciousActivity = await GetRecentSuspiciousActivityAsync(fromDate, toDate);
            
            // Obtener estadísticas de pago
            dashboard.PaymentStats = await GetPaymentStatsAsync(fromDate, toDate);

            return dashboard;
        }

        private async Task<GeneralMetrics> GetGeneralMetricsAsync(DateTime from, DateTime to)
        {
            var activityQuery = _context.ActivityLogs
                .Where(a => a.CreatedAtUtc >= from && a.CreatedAtUtc <= to);

            var requestQuery = _context.RequestLogs
                .Where(r => r.RequestTimeUtc >= from && r.RequestTimeUtc <= to);

            return new GeneralMetrics
            {
                TotalEvents = await activityQuery.CountAsync(),
                UniqueClients = await activityQuery
                    .Where(a => !string.IsNullOrEmpty(a.ClientId))
                    .Select(a => a.ClientId)
                    .Distinct()
                    .CountAsync(),
                UniqueSessions = await activityQuery
                    .Where(a => !string.IsNullOrEmpty(a.SessionId))
                    .Select(a => a.SessionId)
                    .Distinct()
                    .CountAsync(),
                TotalRequests = await requestQuery.CountAsync(),
                AverageResponseTime = (await requestQuery
                    .Where(r => r.ResponseTimeMs.HasValue)
                    .Select(r => r.ResponseTimeMs!.Value)
                    .ToListAsync())
                    .DefaultIfEmpty(0)
                    .Average()
            };
        }

        private async Task<SecurityMetrics> GetSecurityMetricsAsync(DateTime from, DateTime to)
        {
            var metrics = new SecurityMetrics();

            // IPs sospechosas (3+ fallos)
            metrics.SuspiciousIPs = await _context.ActivityLogs
                .Where(a => a.EventType == "payment_failed" && 
                       a.CreatedAtUtc >= from && 
                       a.CreatedAtUtc <= to &&
                       !string.IsNullOrEmpty(a.IpAddress))
                .GroupBy(a => a.IpAddress)
                .Where(g => g.Count() >= 3)
                .CountAsync();

            // Fingerprints compartidos
            metrics.SharedFingerprints = await _context.ActivityLogs
                .Where(a => a.CreatedAtUtc >= from && 
                       a.CreatedAtUtc <= to &&
                       !string.IsNullOrEmpty(a.Fingerprint) &&
                       !string.IsNullOrEmpty(a.ClientId))
                .GroupBy(a => a.Fingerprint)
                .Where(g => g.Select(x => x.ClientId).Distinct().Count() > 1)
                .CountAsync();

            // Clientes con múltiples zonas horarias
            metrics.MultipleTimeZoneClients = await _context.ActivityLogs
                .Where(a => a.CreatedAtUtc >= from && 
                       a.CreatedAtUtc <= to &&
                       !string.IsNullOrEmpty(a.ClientId) &&
                       !string.IsNullOrEmpty(a.TimeZone))
                .GroupBy(a => a.ClientId)
                .Where(g => g.Select(x => x.TimeZone).Distinct().Count() > 1)
                .CountAsync();

            // Intentos rápidos (menos de 5 minutos entre intentos)
            var rapidAttempts = await _context.ActivityLogs
                .Where(a => a.EventType.Contains("payment") && 
                       a.CreatedAtUtc >= from && 
                       a.CreatedAtUtc <= to &&
                       !string.IsNullOrEmpty(a.ClientId))
                .OrderBy(a => a.ClientId)
                .ThenBy(a => a.CreatedAtUtc)
                .ToListAsync();

            metrics.RapidFireAttempts = CountRapidFireAttempts(rapidAttempts);

            // Total de pagos fallidos
            metrics.FailedPayments = await _context.ActivityLogs
                .Where(a => a.EventType == "payment_failed" && 
                       a.CreatedAtUtc >= from && 
                       a.CreatedAtUtc <= to)
                .CountAsync();

            // Calcular puntuación de amenaza (0-100)
            metrics.ThreatScore = CalculateThreatScore(metrics);

            return metrics;
        }

        private int CountRapidFireAttempts(List<ActivityLog> attempts)
        {
            int count = 0;
            var clientGroups = attempts.GroupBy(a => a.ClientId);

            foreach (var group in clientGroups)
            {
                var clientAttempts = group.OrderBy(a => a.CreatedAtUtc).ToList();
                for (int i = 1; i < clientAttempts.Count; i++)
                {
                    var timeDiff = clientAttempts[i].CreatedAtUtc - clientAttempts[i - 1].CreatedAtUtc;
                    if (timeDiff.TotalMinutes < 5)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private double CalculateThreatScore(SecurityMetrics metrics)
        {
            double score = 0;
            
            // Cada IP sospechosa suma 5 puntos
            score += Math.Min(metrics.SuspiciousIPs * 5, 30);
            
            // Fingerprints compartidos suman 10 puntos cada uno
            score += Math.Min(metrics.SharedFingerprints * 10, 30);
            
            // Clientes con múltiples zonas horarias suman 3 puntos
            score += Math.Min(metrics.MultipleTimeZoneClients * 3, 20);
            
            // Intentos rápidos suman 2 puntos
            score += Math.Min(metrics.RapidFireAttempts * 2, 20);

            return Math.Min(score, 100);
        }

        private async Task<List<HourlyActivity>> GetHourlyActivityAsync(DateTime from, DateTime to)
        {
            var activities = await _context.ActivityLogs
                .Where(a => a.CreatedAtUtc >= from && a.CreatedAtUtc <= to)
                .ToListAsync();

            var hourlyData = activities
                .GroupBy(a => new { 
                    Year = a.CreatedAtUtc.Year,
                    Month = a.CreatedAtUtc.Month,
                    Day = a.CreatedAtUtc.Day,
                    Hour = a.CreatedAtUtc.Hour
                })
                .Select(g => new HourlyActivity
                {
                    Hour = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, 0, 0),
                    Events = g.Count(),
                    FailedPayments = g.Count(x => x.EventType == "payment_failed"),
                    SuccessfulPayments = g.Count(x => x.EventType == "payment_success"),
                    UniqueClients = g.Select(x => x.ClientId).Where(id => !string.IsNullOrEmpty(id)).Distinct().Count()
                })
                .OrderBy(h => h.Hour)
                .ToList();

            return hourlyData;
        }

        private async Task<List<TopMetric>> GetTopFailedIPsAsync(DateTime from, DateTime to)
        {
            var failedPayments = await _context.ActivityLogs
                .Where(a => a.EventType == "payment_failed" && 
                       a.CreatedAtUtc >= from && 
                       a.CreatedAtUtc <= to &&
                       !string.IsNullOrEmpty(a.IpAddress))
                .ToListAsync();

            var topIPs = failedPayments
                .GroupBy(a => a.IpAddress)
                .Select(g => new
                {
                    IpAddress = g.Key,
                    Count = g.Count(),
                    LastSeen = g.Max(x => x.CreatedAtUtc),
                    ClientIds = g.Select(x => x.ClientId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList()
                })
                .Where(x => x.Count >= 3)
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();

            return topIPs.Select(ip => new TopMetric
            {
                Identifier = ip.IpAddress,
                Count = ip.Count,
                LastSeen = ip.LastSeen,
                RelatedClients = ip.ClientIds.Where(c => !string.IsNullOrEmpty(c)).ToList(),
                RiskLevel = ip.Count > 10 ? "High" : ip.Count > 5 ? "Medium" : "Low"
            }).ToList();
        }

        private async Task<List<TopMetric>> GetTopSharedFingerprintsAsync(DateTime from, DateTime to)
        {
            var activities = await _context.ActivityLogs
                .Where(a => a.CreatedAtUtc >= from && 
                       a.CreatedAtUtc <= to &&
                       !string.IsNullOrEmpty(a.Fingerprint) &&
                       !string.IsNullOrEmpty(a.ClientId))
                .ToListAsync();

            var sharedFingerprints = activities
                .GroupBy(a => a.Fingerprint)
                .Select(g => new
                {
                    Fingerprint = g.Key,
                    ClientIds = g.Select(x => x.ClientId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList(),
                    EventCount = g.Count(),
                    LastSeen = g.Max(x => x.CreatedAtUtc)
                })
                .Where(x => x.ClientIds.Count > 1)
                .OrderByDescending(x => x.ClientIds.Count)
                .Take(10)
                .ToList();

            return sharedFingerprints.Select(fp => new TopMetric
            {
                Identifier = fp.Fingerprint.Substring(0, 8) + "...", // Mostrar solo los primeros 8 caracteres
                Count = fp.ClientIds.Count,
                LastSeen = fp.LastSeen,
                RelatedClients = fp.ClientIds,
                RiskLevel = fp.ClientIds.Count > 5 ? "High" : fp.ClientIds.Count > 2 ? "Medium" : "Low"
            }).ToList();
        }

        private async Task<List<RecentActivity>> GetRecentSuspiciousActivityAsync(DateTime from, DateTime to)
        {
            var activities = new List<RecentActivity>();

            // Pagos fallidos recientes
            var recentFailures = await _context.ActivityLogs
                .Where(a => a.EventType == "payment_failed" && 
                       a.CreatedAtUtc >= from && 
                       a.CreatedAtUtc <= to)
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(20)
                .Select(a => new RecentActivity
                {
                    Timestamp = a.CreatedAtUtc,
                    EventType = a.EventType,
                    ClientId = a.ClientId ?? "Unknown",
                    IpAddress = a.IpAddress ?? "Unknown",
                    Description = $"Payment failed: {a.ErrorCode ?? "Unknown error"}",
                    Severity = "Warning"
                })
                .ToListAsync();

            activities.AddRange(recentFailures);

            // Detectar actividad de IPs sospechosas
            var suspiciousIPs = await _context.ActivityLogs
                .Where(a => a.CreatedAtUtc >= from && a.CreatedAtUtc <= to)
                .GroupBy(a => new { a.IpAddress, a.ClientId })
                .Where(g => g.Count(x => x.EventType == "payment_failed") >= 3)
                .Select(g => new RecentActivity
                {
                    Timestamp = g.Max(x => x.CreatedAtUtc),
                    EventType = "suspicious_ip",
                    ClientId = g.Key.ClientId ?? "Unknown",
                    IpAddress = g.Key.IpAddress ?? "Unknown",
                    Description = $"Multiple failed attempts from IP: {g.Count()} failures",
                    Severity = "Critical"
                })
                .ToListAsync();

            activities.AddRange(suspiciousIPs);

            return activities.OrderByDescending(a => a.Timestamp).Take(50).ToList();
        }

        private async Task<PaymentMetrics> GetPaymentStatsAsync(DateTime from, DateTime to)
        {
            var paymentEvents = await _context.ActivityLogs
                .Where(a => (a.EventType == "payment_attempt" || 
                            a.EventType == "payment_success" || 
                            a.EventType == "payment_failed") &&
                       a.CreatedAtUtc >= from && 
                       a.CreatedAtUtc <= to)
                .ToListAsync();

            var stats = new PaymentMetrics
            {
                TotalAttempts = paymentEvents.Count(e => e.EventType == "payment_attempt"),
                SuccessfulPayments = paymentEvents.Count(e => e.EventType == "payment_success"),
                FailedPayments = paymentEvents.Count(e => e.EventType == "payment_failed")
            };

            stats.SuccessRate = stats.TotalAttempts > 0 
                ? (double)stats.SuccessfulPayments / stats.TotalAttempts * 100 
                : 0;

            // Suma de montos
            stats.TotalAmount = paymentEvents
                .Where(e => e.Amount.HasValue)
                .Sum(e => e.Amount.Value);

            stats.SuccessAmount = paymentEvents
                .Where(e => e.EventType == "payment_success" && e.Amount.HasValue)
                .Sum(e => e.Amount.Value);

            // Breakdown por método de pago
            stats.PaymentMethodBreakdown = paymentEvents
                .Where(e => !string.IsNullOrEmpty(e.PaymentMethod))
                .GroupBy(e => e.PaymentMethod)
                .ToDictionary(g => g.Key, g => g.Count());

            // Breakdown por código de error
            stats.ErrorCodeBreakdown = paymentEvents
                .Where(e => e.EventType == "payment_failed" && !string.IsNullOrEmpty(e.ErrorCode))
                .GroupBy(e => e.ErrorCode)
                .ToDictionary(g => g.Key, g => g.Count());

            return stats;
        }

        public async Task<LiveDashboard> GetLiveMetricsAsync()
        {
            var now = DateTime.UtcNow;
            var dashboard = new LiveDashboard
            {
                LastUpdate = now
            };

            // Actividad de los últimos 5 minutos
            var recentActivityData = await _context.ActivityLogs
                .Where(a => a.CreatedAtUtc >= now.AddMinutes(-5))
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(20)
                .ToListAsync();

            var recentActivity = recentActivityData
                .Select(a => new RecentActivity
                {
                    Timestamp = a.CreatedAtUtc,
                    EventType = a.EventType,
                    ClientId = a.ClientId ?? "Unknown",
                    IpAddress = a.IpAddress ?? "Unknown",
                    Description = GetEventDescription(a),
                    Severity = GetEventSeverity(a.EventType)
                })
                .ToList();

            dashboard.RecentEvents = recentActivity;

            // Sesiones activas (últimos 30 minutos)
            dashboard.ActiveSessions = await _context.ActivityLogs
                .Where(a => a.CreatedAtUtc >= now.AddMinutes(-30) && !string.IsNullOrEmpty(a.SessionId))
                .Select(a => a.SessionId)
                .Distinct()
                .CountAsync();

            // Eventos por minuto
            dashboard.EventsPerMinute = await _context.ActivityLogs
                .Where(a => a.CreatedAtUtc >= now.AddMinutes(-1))
                .CountAsync();

            // Métricas en tiempo real con comparación
            dashboard.Metrics = await GetRealTimeMetricsAsync();

            return dashboard;
        }

        private async Task<List<RealTimeMetric>> GetRealTimeMetricsAsync()
        {
            var now = DateTime.UtcNow;
            var metrics = new List<RealTimeMetric>();

            // Eventos en la última hora vs hora anterior
            var currentHourEvents = await _context.ActivityLogs
                .Where(a => a.CreatedAtUtc >= now.AddHours(-1))
                .CountAsync();

            var previousHourEvents = await _context.ActivityLogs
                .Where(a => a.CreatedAtUtc >= now.AddHours(-2) && a.CreatedAtUtc < now.AddHours(-1))
                .CountAsync();

            metrics.Add(CreateRealTimeMetric("events_per_hour", "Events/Hour", 
                currentHourEvents, previousHourEvents));

            // Tasa de éxito de pagos
            var currentPayments = await _context.ActivityLogs
                .Where(a => (a.EventType == "payment_success" || a.EventType == "payment_failed") &&
                       a.CreatedAtUtc >= now.AddHours(-1))
                .ToListAsync();

            var currentSuccessRate = currentPayments.Any() 
                ? (double)currentPayments.Count(p => p.EventType == "payment_success") / currentPayments.Count * 100 
                : 0;

            var previousPayments = await _context.ActivityLogs
                .Where(a => (a.EventType == "payment_success" || a.EventType == "payment_failed") &&
                       a.CreatedAtUtc >= now.AddHours(-2) && a.CreatedAtUtc < now.AddHours(-1))
                .ToListAsync();

            var previousSuccessRate = previousPayments.Any() 
                ? (double)previousPayments.Count(p => p.EventType == "payment_success") / previousPayments.Count * 100 
                : 0;

            metrics.Add(CreateRealTimeMetric("payment_success_rate", "Payment Success Rate", 
                currentSuccessRate, previousSuccessRate));

            // Tiempo de respuesta promedio
            var currentResponseTimes = await _context.RequestLogs
                .Where(r => r.RequestTimeUtc >= now.AddMinutes(-5) && r.ResponseTimeMs.HasValue)
                .Select(r => r.ResponseTimeMs!.Value)
                .ToListAsync();
            
            var currentResponseTime = currentResponseTimes.Any() 
                ? currentResponseTimes.Average() 
                : 0.0;

            var previousResponseTimes = await _context.RequestLogs
                .Where(r => r.RequestTimeUtc >= now.AddMinutes(-10) && 
                       r.RequestTimeUtc < now.AddMinutes(-5) && 
                       r.ResponseTimeMs.HasValue)
                .Select(r => r.ResponseTimeMs!.Value)
                .ToListAsync();
            
            var previousResponseTime = previousResponseTimes.Any() 
                ? previousResponseTimes.Average() 
                : 0.0;

            metrics.Add(CreateRealTimeMetric("avg_response_time", "Avg Response Time (ms)", 
                currentResponseTime, previousResponseTime));

            return metrics;
        }

        private RealTimeMetric CreateRealTimeMetric(string type, string label, double current, double previous)
        {
            var changePercent = previous > 0 ? ((current - previous) / previous) * 100 : 0;
            
            return new RealTimeMetric
            {
                MetricType = type,
                Label = label,
                Value = current,
                PreviousValue = previous,
                ChangePercent = Math.Round(changePercent, 2),
                Trend = changePercent > 5 ? "up" : changePercent < -5 ? "down" : "stable",
                Timestamp = DateTime.UtcNow
            };
        }

        private string GetEventDescription(ActivityLog log)
        {
            return log.EventType switch
            {
                "payment_success" => $"Payment successful: {log.Currency} {log.Amount:F2}",
                "payment_failed" => $"Payment failed: {log.ErrorCode ?? "Unknown error"}",
                "payment_attempt" => $"Payment attempt: {log.PaymentMethod ?? "Unknown method"}",
                "session_start" => "New session started",
                "session_end" => "Session ended",
                _ => log.EventType.Replace('_', ' ')
            };
        }

        private string GetEventSeverity(string eventType)
        {
            return eventType switch
            {
                "payment_failed" => "Warning",
                "suspicious_ip" => "Critical",
                "payment_success" => "Info",
                _ => "Info"
            };
        }

        public async Task<object> GetSuspiciousActivitiesAsync(int hours = 24)
        {
            // Reutilizar la lógica del controlador existente
            var from = DateTime.UtcNow.AddHours(-hours);
            var to = DateTime.UtcNow;
            
            // Implementación simplificada, podrías reutilizar el código del ActivityLogController
            return new { message = "Not implemented yet" };
        }
    }
}