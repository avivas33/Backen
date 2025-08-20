using Microsoft.AspNetCore.Mvc;
using Api_Celero.Models;
using Api_Celero.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Api_Celero.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecibosController : ControllerBase
    {
        private readonly IHansaReceiptService _hansaReceiptService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RecibosController> _logger;

        public RecibosController(
            IHansaReceiptService hansaReceiptService,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<RecibosController> logger)
        {
            _hansaReceiptService = hansaReceiptService;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("enviar-notificacion")]
        public async Task<IActionResult> EnviarNotificacionRecibo([FromBody] SendReceiptEmailRequest request)
        {
            try
            {
                // Validar request
                if (string.IsNullOrEmpty(request.ReceiptNumber))
                {
                    return BadRequest(new { error = "El número de recibo es requerido" });
                }

                if (string.IsNullOrEmpty(request.CompanyCode))
                {
                    return BadRequest(new { error = "El código de empresa es requerido" });
                }

                if (string.IsNullOrEmpty(request.CustomerEmail))
                {
                    return BadRequest(new { error = "El email del cliente es requerido" });
                }

                _logger.LogInformation($"Procesando notificación de recibo {request.ReceiptNumber} para empresa {request.CompanyCode}");

                // Consultar la API de Hansa
                var receiptData = await _hansaReceiptService.GetReceiptDataAsync(request.ReceiptNumber, request.CompanyCode);

                if (receiptData == null || receiptData.Data?.IPVc == null || !receiptData.Data.IPVc.Any())
                {
                    _logger.LogWarning($"No se encontraron datos para el recibo {request.ReceiptNumber}");
                    return NotFound(new { error = $"No se encontraron datos para el recibo {request.ReceiptNumber}" });
                }

                var ipcvData = receiptData.Data.IPVc.First();

                // Validar que hay datos
                if (ipcvData.Rows == null || !ipcvData.Rows.Any())
                {
                    _logger.LogWarning($"El recibo {request.ReceiptNumber} no tiene detalles");
                    return BadRequest(new { error = "El recibo no contiene detalles de pago" });
                }

                // Obtener el nombre del cliente del primer row (todos deberían tener el mismo cliente)
                var customerName = ipcvData.Rows.First().CustName;

                // Parsear la fecha de transacción
                DateTime transDate;
                if (!DateTime.TryParse(ipcvData.TransDate, out transDate))
                {
                    transDate = DateTime.Now;
                }

                // Formatear la fecha en español
                var culture = new CultureInfo("es-ES");
                var formattedDate = transDate.ToString("d 'de' MMMM, yyyy", culture);

                // Parsear el monto total
                decimal totalAmount;
                if (!decimal.TryParse(ipcvData.CurPayVal, NumberStyles.Any, CultureInfo.InvariantCulture, out totalAmount))
                {
                    _logger.LogError($"No se pudo parsear el monto total: {ipcvData.CurPayVal}");
                    totalAmount = 0;
                }

                // Mapear el método de pago (PayMode puede venir vacío o null, usar el campo si está disponible)
                var paymentMethod = !string.IsNullOrEmpty(ipcvData.PayMode) ? ipcvData.PayMode : "PAGO";

                // Convertir el método de pago a un formato más amigable
                paymentMethod = paymentMethod.ToUpper() switch
                {
                    "PAYPAL" => "PAYPAL",
                    "ACH" => "ACH",
                    "YAPPY" => "YAPPY",
                    "TARJETA" => "TARJETA DE CRÉDITO",
                    "CREDIT" => "TARJETA DE CRÉDITO",
                    "CARD" => "TARJETA DE CRÉDITO",
                    _ => paymentMethod
                };

                // Preparar los detalles del recibo
                var details = ipcvData.Rows.Select(row =>
                {
                    decimal recVal;
                    if (!decimal.TryParse(row.RecVal, NumberStyles.Any, CultureInfo.InvariantCulture, out recVal))
                    {
                        recVal = 0;
                    }

                    return new ReceiptDetailItem
                    {
                        Reference = row.InvoiceNr,
                        CufeNumber = row.InvoiceOfficialSerNr,
                        Quota = "0", // Siempre 0 según lo indicado
                        ReceivedAmount = recVal
                    };
                }).ToList();

                // Crear la solicitud de email
                var emailRequest = new ReceiptEmailRequest
                {
                    ReceiptNumber = request.ReceiptNumber,
                    CompanyCode = request.CompanyCode,
                    CustomerEmail = request.CustomerEmail,
                    CustomerName = customerName,
                    TransactionDate = formattedDate,
                    PaymentMethod = paymentMethod,
                    TotalAmount = totalAmount,
                    Details = details
                };

                // Enviar el email
                var emailResult = await _emailService.SendReceiptEmailAsync(emailRequest);

                if (emailResult.Success)
                {
                    _logger.LogInformation($"Notificación de recibo {request.ReceiptNumber} enviada exitosamente a {request.CustomerEmail}");
                    
                    return Ok(new
                    {
                        success = true,
                        message = "Notificación enviada exitosamente",
                        data = new
                        {
                            receiptNumber = request.ReceiptNumber,
                            customerEmail = request.CustomerEmail,
                            customerName = customerName,
                            totalAmount = totalAmount,
                            transactionDate = formattedDate,
                            paymentMethod = paymentMethod,
                            detailsCount = details.Count,
                            emailId = emailResult.EmailId
                        }
                    });
                }
                else
                {
                    _logger.LogError($"Error al enviar notificación de recibo {request.ReceiptNumber}: {emailResult.Error}");
                    return StatusCode(500, new { error = "Error al enviar la notificación", details = emailResult.Error });
                }
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, $"Timeout al procesar recibo {request.ReceiptNumber}");
                return StatusCode(504, new { error = "La consulta del recibo excedió el tiempo límite" });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Error HTTP al procesar recibo {request.ReceiptNumber}");
                return StatusCode(502, new { error = "Error al comunicarse con el sistema de recibos" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error inesperado al procesar recibo {request.ReceiptNumber}");
                return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        [HttpGet("consultar/{receiptNumber}")]
        public async Task<IActionResult> ConsultarRecibo(string receiptNumber, [FromQuery] string companyCode = "2")
        {
            try
            {
                if (string.IsNullOrEmpty(receiptNumber))
                {
                    return BadRequest(new { error = "El número de recibo es requerido" });
                }

                _logger.LogInformation($"Consultando recibo {receiptNumber} para empresa {companyCode}");

                // Consultar la API de Hansa
                var receiptData = await _hansaReceiptService.GetReceiptDataAsync(receiptNumber, companyCode);

                if (receiptData == null || receiptData.Data?.IPVc == null || !receiptData.Data.IPVc.Any())
                {
                    return NotFound(new { error = $"No se encontraron datos para el recibo {receiptNumber}" });
                }

                return Ok(receiptData);
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, $"Timeout al consultar recibo {receiptNumber}");
                return StatusCode(504, new { error = "La consulta del recibo excedió el tiempo límite" });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Error HTTP al consultar recibo {receiptNumber}");
                return StatusCode(502, new { error = "Error al comunicarse con el sistema de recibos" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error inesperado al consultar recibo {receiptNumber}");
                return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
            }
        }
    }
}