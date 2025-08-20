using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Api_Celero.Models;
using System.Text.Json;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Api_Celero.Services;

namespace Api_Celero.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ActivityLogController : ControllerBase
    {
        private readonly ActivityLogContext _context;
        private readonly ILogger<ActivityLogController> _logger;
        private readonly IMetricsService _metricsService;

        public ActivityLogController(
            ActivityLogContext context, 
            ILogger<ActivityLogController> logger,
            IMetricsService metricsService)
        {
            _context = context;
            _logger = logger;
            _metricsService = metricsService;
        }

        [HttpPost("track")]
        public async Task<IActionResult> TrackActivity([FromBody] ActivityLogRequest request)
        {
            try
            {
                // Validar la petición
                if (request == null)
                {
                    return BadRequest(new { error = "Invalid request" });
                }

                // Crear el registro de actividad
                var activityLog = new ActivityLog
                {
                    EventType = request.EventType,
                    Fingerprint = request.Fingerprint,
                    IpAddress = GetClientIpAddress(),
                    UserAgent = Request.Headers["User-Agent"].ToString(),
                    TimeZone = request.TimeZone,
                    ScreenResolution = request.ScreenResolution,
                    BrowserLanguage = request.BrowserLanguage,
                    ClientId = request.ClientId,
                    SessionId = request.SessionId,
                    PaymentMethod = request.PaymentMethod,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    PaymentStatus = request.PaymentStatus,
                    ErrorCode = request.ErrorCode,
                    CreatedAtUtc = DateTime.UtcNow
                };

                // Agregar datos adicionales si existen
                if (request.AdditionalData != null)
                {
                    activityLog.AdditionalData = JsonSerializer.Serialize(request.AdditionalData);
                }

                // Guardar en la base de datos
                _context.ActivityLogs.Add(activityLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Activity tracked: {request.EventType} for client {request.ClientId}");

                return Ok(new { success = true, id = activityLog.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking activity");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("analytics/suspicious")]
        public async Task<IActionResult> GetSuspiciousActivity([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            try
            {
                var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
                var toDate = to ?? DateTime.UtcNow;

                // Múltiples intentos de pago fallidos desde la misma IP
                var failedPaymentsPerIp = await _context.ActivityLogs
                    .Where(a => a.EventType == "payment_failed" && 
                           a.CreatedAtUtc >= fromDate && 
                           a.CreatedAtUtc <= toDate &&
                           !string.IsNullOrEmpty(a.IpAddress))
                    .GroupBy(a => a.IpAddress)
                    .Select(g => new
                    {
                        IpAddress = g.Key,
                        FailedAttempts = g.Count(),
                        ClientIds = g.Select(a => a.ClientId).Distinct().Count(),
                        LastAttempt = g.Max(a => a.CreatedAtUtc)
                    })
                    .Where(x => x.FailedAttempts >= 3)
                    .OrderByDescending(x => x.FailedAttempts)
                    .ToListAsync();

                // Diferentes cuentas con el mismo fingerprint
                var sharedFingerprints = await _context.ActivityLogs
                    .Where(a => a.CreatedAtUtc >= fromDate && 
                           a.CreatedAtUtc <= toDate &&
                           !string.IsNullOrEmpty(a.Fingerprint) &&
                           !string.IsNullOrEmpty(a.ClientId))
                    .GroupBy(a => a.Fingerprint)
                    .Select(g => new
                    {
                        Fingerprint = g.Key,
                        ClientCount = g.Select(a => a.ClientId).Distinct().Count(),
                        ClientIds = g.Select(a => a.ClientId).Distinct().ToList(),
                        EventCount = g.Count()
                    })
                    .Where(x => x.ClientCount > 1)
                    .OrderByDescending(x => x.ClientCount)
                    .ToListAsync();

                // Accesos desde zonas horarias inusuales
                var unusualTimeZones = await _context.ActivityLogs
                    .Where(a => a.CreatedAtUtc >= fromDate && 
                           a.CreatedAtUtc <= toDate &&
                           !string.IsNullOrEmpty(a.TimeZone))
                    .GroupBy(a => new { a.ClientId, a.TimeZone })
                    .Select(g => new
                    {
                        ClientId = g.Key.ClientId,
                        TimeZone = g.Key.TimeZone,
                        AccessCount = g.Count(),
                        LastAccess = g.Max(a => a.CreatedAtUtc)
                    })
                    .ToListAsync();

                // Agrupar por cliente para detectar múltiples zonas horarias
                var clientsWithMultipleTimeZones = unusualTimeZones
                    .GroupBy(x => x.ClientId)
                    .Where(g => g.Count() > 1)
                    .Select(g => new
                    {
                        ClientId = g.Key,
                        TimeZones = g.Select(x => x.TimeZone).ToList(),
                        TotalAccesses = g.Sum(x => x.AccessCount)
                    })
                    .ToList();

                return Ok(new
                {
                    failedPaymentsPerIp,
                    sharedFingerprints,
                    clientsWithMultipleTimeZones,
                    period = new { from = fromDate, to = toDate }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting suspicious activity");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        private string GetClientIpAddress()
        {
            var headers = Request.Headers;
            
            // Orden de prioridad para headers de IP
            string[] headerNames = { "X-Real-IP", "X-Forwarded-For", "CF-Connecting-IP", "X-Original-For" };
            
            foreach (var headerName in headerNames)
            {
                if (headers.TryGetValue(headerName, out var value))
                {
                    var ip = value.ToString();
                    
                    // X-Forwarded-For puede contener múltiples IPs
                    if (headerName == "X-Forwarded-For" && ip.Contains(","))
                    {
                        ip = ip.Split(',').First().Trim();
                    }
                    
                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        return ip;
                    }
                }
            }
            
            // Si no hay headers de proxy, usar la IP de la conexión
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            try
            {
                var dashboard = await _metricsService.GetDashboardMetricsAsync(from, to);
                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard metrics");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("dashboard/live")]
        public async Task<IActionResult> GetLiveDashboard()
        {
            try
            {
                var liveDashboard = await _metricsService.GetLiveMetricsAsync();
                return Ok(liveDashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting live dashboard");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("metrics/stream")]
        public async Task StreamMetrics()
        {
            Response.Headers.Add("Content-Type", "text/event-stream");
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Connection", "keep-alive");

            var cts = HttpContext.RequestAborted;

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var metrics = await _metricsService.GetLiveMetricsAsync();
                    var json = JsonSerializer.Serialize(metrics, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    await Response.WriteAsync($"data: {json}\n\n");
                    await Response.Body.FlushAsync();

                    // Esperar 5 segundos antes de enviar la siguiente actualización
                    await Task.Delay(5000, cts);
                }
            }
            catch (TaskCanceledException)
            {
                // Cliente desconectado, es normal
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming metrics");
            }
        }
    }

    public class ActivityLogRequest
    {
        public string EventType { get; set; } = string.Empty;
        public string? Fingerprint { get; set; }
        public string? TimeZone { get; set; }
        public string? ScreenResolution { get; set; }
        public string? BrowserLanguage { get; set; }
        public string? ClientId { get; set; }
        public string? SessionId { get; set; }
        public string? PaymentMethod { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public string? PaymentStatus { get; set; }
        public string? ErrorCode { get; set; }
        public Dictionary<string, object>? AdditionalData { get; set; }
    }
}