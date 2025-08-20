using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using Resend;
using Api_Celero.Services;
using System;
using System.Linq;
using Api_Celero;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Api_Celero.Models;
using Api_Celero.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configurar para leer variables de entorno
builder.Configuration.AddEnvironmentVariables();

// Configurar URLs por defecto si no se especifica ASPNETCORE_URLS
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "7262";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configuración CORS mejorada para producción
builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionCors", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
        
        // Si no se especifican orígenes en la configuración, usar los valores por defecto
        if (allowedOrigins == null || allowedOrigins.Length == 0)
        {
            allowedOrigins = new[] { 
                "http://localhost:8080", 
                "https://localhost:8080",
                "http://localhost:3000",
                "https://localhost:3000", 
                "https://selfservice-dev.celero.network",
                "https://selfservice.celero.network",
                "https://celero.network"
            };
        }
        
        // También considerar la variable de entorno ALLOWED_ORIGINS
        var envOrigins = builder.Configuration["ALLOWED_ORIGINS"];
        if (!string.IsNullOrEmpty(envOrigins))
        {
            var envOriginsList = envOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(o => o.Trim())
                                          .ToArray();
            allowedOrigins = allowedOrigins.Concat(envOriginsList).Distinct().ToArray();
        }
        
        // Registrar los orígenes permitidos para depuración
        Console.WriteLine("=== CORS Configuration ===");
        foreach (var origin in allowedOrigins)
        {
            Console.WriteLine($"CORS: Origen permitido: {origin}");
        }
        Console.WriteLine("=========================");
        
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromSeconds(1728000)); // 20 días
    });
});

// Registrar HttpClient para inyección de dependencias
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IResend>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("Resend");
    var apiKey = configuration["Resend:ApiKey"] ?? throw new InvalidOperationException("Resend ApiKey is required");
    
    var options = new ResendClientOptions { ApiToken = apiKey };
    
    // Crear un IOptionsSnapshot usando nuestro wrapper personalizado
    IOptionsSnapshot<ResendClientOptions> optionsSnapshot = new SimpleOptionsSnapshot<ResendClientOptions>(options);
    
    return new ResendClient(optionsSnapshot, httpClient);
});

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<PdfDownloaderService>();
builder.Services.AddScoped<IMetricsService, MetricsService>();
builder.Services.AddScoped<IHansaReceiptService, HansaReceiptService>();

// Configurar opciones de pago con soporte para variables de entorno
builder.Services.Configure<Api_Celero.Models.CobaltSettings>(options =>
{
    // Inicializar el diccionario antes de hacer bind
    options.Companies = new Dictionary<string, Api_Celero.Models.CobaltCompanyCredentials>();
    
    builder.Configuration.GetSection("Cobalt").Bind(options);
    
    // Verificar que el diccionario se haya cargado correctamente
    Console.WriteLine($"Cobalt Companies loaded: {options.Companies?.Count ?? 0}");
    
    // Las credenciales ahora están organizadas por empresa en appsettings.json
    // Se pueden sobrescribir con variables de entorno si es necesario
    // Formato: COBALT_COMPANY_{COMPCODE}_CLIENT_ID y COBALT_COMPANY_{COMPCODE}_CLIENT_SECRET
});

builder.Services.Configure<Api_Celero.Models.YappySettings>(options =>
{
    builder.Configuration.GetSection("Yappy").Bind(options);
    
    // Sobrescribir con variables de entorno si existen
    var envMerchantId = Environment.GetEnvironmentVariable("YAPPY_MERCHANT_ID");
    
    if (!string.IsNullOrEmpty(envMerchantId))
        options.MerchantId = envMerchantId;
});

builder.Services.Configure<Api_Celero.Models.PayPalSettings>(options =>
{
    builder.Configuration.GetSection("PayPal").Bind(options);
    
    // Sobrescribir con variables de entorno si existen
    var envClientId = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID");
    var envClientSecret = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET");
    var envMode = Environment.GetEnvironmentVariable("PAYPAL_MODE");
    
    if (!string.IsNullOrEmpty(envClientId))
        options.ClientId = envClientId;
    if (!string.IsNullOrEmpty(envClientSecret))
        options.ClientSecret = envClientSecret;
    if (!string.IsNullOrEmpty(envMode))
        options.Mode = envMode;
});

// Configurar opciones de WebPOS
builder.Services.Configure<Api_Celero.Models.WebPOSSettings>(options =>
{
    builder.Configuration.GetSection("WebPOS").Bind(options);
});

// Configuración SQLite para recibos offline
var sqliteConnectionString = builder.Configuration.GetConnectionString("RecibosOffline") ?? "Data Source=recibos_offline.db";
builder.Services.AddSqliteDb(sqliteConnectionString);

// Configuración SQLite para Activity Logs
var activityLogConnectionString = builder.Configuration.GetConnectionString("ActivityLogs") ?? "Data Source=activity_logs.db";
builder.Services.AddDbContext<ActivityLogContext>(options =>
    options.UseSqlite(activityLogConnectionString));

// Configurar opciones de Hansa
builder.Services.Configure<Api_Celero.Models.HansaSettings>(options =>
{
    builder.Configuration.GetSection("Hansa").Bind(options);
});

// Configurar opciones de ACH Instructions
builder.Services.Configure<Api_Celero.Models.ACHInstructionsSettings>(options =>
{
    builder.Configuration.GetSection("ACHInstructions").Bind(options);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// IMPORTANTE: El orden del middleware es crítico
// 1. CORS debe ir ANTES de UseHttpsRedirection
// 2. CORS debe ir ANTES de UseAuthorization
// 3. CORS debe ir DESPUÉS de UseRouting (si se usa)

// Aplicar la política CORS
app.UseCors("ProductionCors");

// Agregar middleware de logging de peticiones
app.UseRequestLogging();

// Luego aplicar redirección HTTPS
app.UseHttpsRedirection();

// Finalmente autorización
app.UseAuthorization();

// Mapear controladores
app.MapControllers();

// Logging para verificar que CORS está activo
app.Use(async (context, next) =>
{
    // Log de peticiones OPTIONS para debugging
    if (context.Request.Method == "OPTIONS")
    {
        Console.WriteLine($"OPTIONS request from origin: {context.Request.Headers["Origin"]}");
        Console.WriteLine($"Requested headers: {context.Request.Headers["Access-Control-Request-Headers"]}");
        Console.WriteLine($"Requested method: {context.Request.Headers["Access-Control-Request-Method"]}");
    }
    
    await next();
    
    // Log de respuestas CORS
    if (context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
    {
        Console.WriteLine($"CORS headers sent for origin: {context.Response.Headers["Access-Control-Allow-Origin"]}");
    }
});

app.Run();