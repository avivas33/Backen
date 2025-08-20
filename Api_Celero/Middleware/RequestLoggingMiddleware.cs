using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Api_Celero.Models;
using System.Linq;

namespace Api_Celero.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();

            // Capturar datos de la petición
            var requestLog = new RequestLog
            {
                IpAddress = GetClientIpAddress(context),
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                Method = context.Request.Method,
                Path = context.Request.Path,
                QueryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                RequestTimeUtc = DateTime.UtcNow
            };

            // Intentar obtener ClientId de los headers
            if (context.Request.Headers.TryGetValue("X-Client-Id", out var clientId))
            {
                requestLog.ClientId = clientId.ToString();
            }

            try
            {
                // Procesar la petición
                await _next(context);
                
                requestLog.StatusCode = context.Response.StatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando petición");
                requestLog.StatusCode = 500;
                throw;
            }
            finally
            {
                sw.Stop();
                requestLog.ResponseTimeMs = sw.ElapsedMilliseconds;

                // Guardar el log inmediatamente
                try
                {
                    using var scope = context.RequestServices.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ActivityLogContext>();
                    
                    dbContext.RequestLogs.Add(requestLog);
                    await dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error guardando log de petición");
                }
            }
        }

        private string GetClientIpAddress(HttpContext context)
        {
            // Intentar obtener la IP real del cliente considerando proxies
            var headers = context.Request.Headers;
            
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
                    
                    if (!string.IsNullOrWhiteSpace(ip) && IsValidIp(ip))
                    {
                        return ip;
                    }
                }
            }
            
            // Si no hay headers de proxy, usar la IP de la conexión
            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private bool IsValidIp(string ip)
        {
            // Validación básica de IP
            if (string.IsNullOrWhiteSpace(ip)) return false;
            
            // IPv4
            if (System.Net.IPAddress.TryParse(ip, out var address))
            {
                return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ||
                       address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
            }
            
            return false;
        }
    }

    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}