using Microsoft.AspNetCore.Mvc;
using Api_Celero.Models;
using Api_Celero.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Api_Celero.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VerificacionController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly ReciboCajaOfflineContext _dbContext;
        private readonly ILogger<VerificacionController> _logger;

        public VerificacionController(
            IEmailService emailService,
            ReciboCajaOfflineContext dbContext,
            ILogger<VerificacionController> logger)
        {
            _emailService = emailService;
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpPost("solicitar-codigo")]
        public async Task<IActionResult> SolicitarCodigo([FromBody] SolicitudCodigoVerificacion request)
        {
            try
            {
                // Validar request
                if (string.IsNullOrEmpty(request.Email) || 
                    string.IsNullOrEmpty(request.ClienteCode) || 
                    string.IsNullOrEmpty(request.TipoConsulta))
                {
                    return BadRequest("Datos incompletos");
                }

                // Limpiar códigos expirados
                await LimpiarCodigosExpirados();

                // Verificar si ya existe un código activo para este email y cliente
                var codigoExistente = await _dbContext.CodigosVerificacion
                    .FirstOrDefaultAsync(c => c.Email == request.Email && 
                                            c.ClienteCode == request.ClienteCode && 
                                            c.TipoConsulta == request.TipoConsulta &&
                                            !c.Usado && 
                                            c.FechaExpiracion > DateTime.UtcNow);

                if (codigoExistente != null)
                {
                    return BadRequest("Ya existe un código activo para este usuario. Espere a que expire.");
                }

                // Generar código de 4 dígitos
                string codigo = GenerarCodigoAleatorio();

                // Crear registro en base de datos
                var nuevoCodigo = new CodigoVerificacion
                {
                    Email = request.Email,
                    Codigo = codigo,
                    ClienteCode = request.ClienteCode,
                    TipoConsulta = request.TipoConsulta,
                    FechaCreacion = DateTime.UtcNow,
                    FechaExpiracion = DateTime.UtcNow.AddMinutes(2), // 2 minutos de expiración
                    Usado = false
                };

                _dbContext.CodigosVerificacion.Add(nuevoCodigo);
                await _dbContext.SaveChangesAsync();

                // Enviar email
                string asunto = $"Código de Acceso al Portal de Celero es {codigo}";
                string cuerpoHtml = GenerarEmailVerificacion(codigo, request.TipoConsulta);

                // Log para debugging - verificar el contenido HTML generado
                _logger.LogInformation($"HTML generado para email de verificación (primeros 500 caracteres): {cuerpoHtml.Substring(0, Math.Min(500, cuerpoHtml.Length))}");

                // TEMPORAL: Solo para pruebas - enviar a alexis.vivas@gmail.com
               

                            var emailRequest = new EmailRequest
            {
                From = "noreply@celero.net", // Usa tu dominio verificado
                To =  request.Email, // TEMPORAL: cambiar por request.Email después de las pruebas
                Subject = asunto,
                HtmlContent = cuerpoHtml,
                TextContent = GenerarEmailTexto(codigo, "login"), // CRÍTICO para evitar spam
                Headers = new Dictionary<string, string>
                {
                    { "X-Priority", "3" },
                    { "X-Mailer", "Celero Verification System" },
                    { "List-Unsubscribe", "<mailto:unsubscribe@celero.net>" }
                }
            };

            await _emailService.SendEmailAsync(emailRequest);

                _logger.LogInformation($"Código de verificación enviado a {request.Email} para cliente {request.ClienteCode}");

                return Ok(new { mensaje = "Código enviado exitosamente", email = request.Email });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar código de verificación");
                return StatusCode(500, "Error interno del servidor");
            }
        }


// Nueva función para generar versión texto
string GenerarEmailTexto(string codigo, string tipoConsulta)
{
    return $@"Hola,

Se ha solicitado iniciar sesión en tu cuenta. Para continuar, utiliza este código:

{codigo}

Para más información sobre por qué recibiste este correo, consulta nuestros términos y condiciones en: https://celero.net/docs/tyc-portal

Saludos,
Celero";
}



        [HttpPost("verificar-codigo")]
        public async Task<IActionResult> VerificarCodigo([FromBody] VerificarCodigoRequest request)
        {
            try
            {
                // Validar request
                if (string.IsNullOrEmpty(request.Email) || 
                    string.IsNullOrEmpty(request.Codigo) || 
                    string.IsNullOrEmpty(request.ClienteCode))
                {
                    return BadRequest("Datos incompletos");
                }

                // Buscar código en base de datos
                var codigoVerificacion = await _dbContext.CodigosVerificacion
                    .FirstOrDefaultAsync(c => c.Email == request.Email && 
                                            c.Codigo == request.Codigo && 
                                            c.ClienteCode == request.ClienteCode &&
                                            c.TipoConsulta == request.TipoConsulta &&
                                            !c.Usado && 
                                            c.FechaExpiracion > DateTime.UtcNow);

                if (codigoVerificacion == null)
                {
                    return BadRequest("Código inválido o expirado");
                }

                // Marcar código como usado
                codigoVerificacion.Usado = true;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Código verificado exitosamente para {request.Email}");

                return Ok(new { 
                    mensaje = "Código verificado exitosamente", 
                    tipoConsulta = request.TipoConsulta,
                    clienteCode = request.ClienteCode 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar código");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        private string GenerarCodigoAleatorio()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);

                string codigo = "";
                for (int i = 0; i < 4; i++)
                {
                    codigo += (bytes[i] % 10).ToString();
                }
                return codigo;
            }
        }

        private string GenerarEmailVerificacion(string codigo, string tipoConsulta)
{
    return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Nuevo Email de Verificación - Celero</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 20px;
            background-color: #f8f9fa;
            line-height: 1.6;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: white;
            border-radius: 12px;
            overflow: hidden;
            box-shadow: 0 4px 20px rgba(0,0,0,0.15);
        }}
        .header {{
            background-color: #ffffff;
            padding: 30px 20px 0px 24px;
            text-align: left;
        }}
        .logo {{
            width: 150px;
            height: auto;
        }}
        .content {{
            padding: 10px 30px 25px 30px;
            text-align: left;
        }}
        .codigo-verificacion-num {{
            font-size: 25px;
            font-weight: bold;
            color: #7c3aed;
            margin-left: -1px;
        }}
        .main-text {{
            color: #374151;
            font-size: 16px;
            line-height: 1.4;
            margin: 0;
            text-align: left;
        }}
        .main-text a {{
            color: #7c3aed;
            text-decoration: underline;
        }}
        .main-text a:hover {{
            color: #5b21b6;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <img src='https://i.imgur.com/5JcnxGm.png' alt='Celero' class='logo' style='width: 150px; height: auto; display: block;' />
        </div>
         <div class='content'>
            <div class='main-text'>
                <br>Hola,<br><br>
                Se ha solicitado iniciar sesión en tu cuenta. Para continuar, utiliza este código:<br><br>
                <span class='codigo-verificacion-num'>{codigo}</span><br><br>
                Para más información sobre por qué recibiste este correo, consulta el artículo de ayuda sobre los <a href='https://celero.net/docs/tyc-portal' target='_blank'>términos y condiciones</a> de nuestro portal.<br><br>
                Saludos,<br>
                Celero
            </div>
        </div>
    </div>
</body>
</html>";
}


        private async Task LimpiarCodigosExpirados()
        {
            var codigosExpirados = await _dbContext.CodigosVerificacion
                .Where(c => c.FechaExpiracion < DateTime.UtcNow)
                .ToListAsync();

            if (codigosExpirados.Any())
            {
                _dbContext.CodigosVerificacion.RemoveRange(codigosExpirados);
                await _dbContext.SaveChangesAsync();
            }
        }
    }
}