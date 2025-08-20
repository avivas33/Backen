using Resend;
using Api_Celero.Models;

namespace Api_Celero.Services
{
    public interface IEmailService
    {
        Task<EmailResponse> SendEmailAsync(EmailRequest emailRequest);
        Task<EmailResponse> SendPaymentConfirmationAsync(PaymentConfirmationEmailRequest request);
        Task<EmailResponse> SendInvoiceReminderAsync(InvoiceReminderEmailRequest request);
        Task<EmailResponse> SendWelcomeEmailAsync(WelcomeEmailRequest request);
        Task<EmailResponse> SendReceiptEmailAsync(ReceiptEmailRequest request);
    }    public class EmailService : IEmailService
    {
        private readonly IResend _resend;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IResend resend, IConfiguration configuration, ILogger<EmailService> logger)
        {
            _resend = resend;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<EmailResponse> SendEmailAsync(EmailRequest emailRequest)        {
            try
            {
                var fromEmail = emailRequest.From ?? _configuration["Resend:DefaultFromEmail"] ?? "noreply@tu-dominio.com";
                var fromName = emailRequest.FromName ?? _configuration["Resend:DefaultFromName"] ?? "Celero";

                var message = new EmailMessage
                {
                    From = $"{fromName} <{fromEmail}>",
                    To = new[] { emailRequest.To },
                    Subject = emailRequest.Subject,
                };

                // Agregar contenido
                if (!string.IsNullOrEmpty(emailRequest.HtmlContent))
                {
                    message.HtmlBody = emailRequest.HtmlContent;
                }
                
                if (!string.IsNullOrEmpty(emailRequest.TextContent))
                {
                    message.TextBody = emailRequest.TextContent;
                }

                // Agregar attachments si existen
                if (emailRequest.Attachments?.Any() == true)
                {
                    message.Attachments = emailRequest.Attachments.Select(a => new Resend.EmailAttachment
                    {
                        Filename = a.Filename,
                        ContentType = a.ContentType,
                        Content = Convert.FromBase64String(a.Content)
                    }).ToList();
                }

                // Agregar tags si existen (Resend 0.1.4 puede no soportar tags directamente)
                if (emailRequest.Tags?.Any() == true)
                {
                    // Para versiones más nuevas de Resend, esto puede cambiar
                    // message.Tags = emailRequest.Tags;
                }

                var response = await _resend.EmailSendAsync(message);

                // La respuesta de Resend 0.1.4 es diferente
                if (response != null)
                {
                    _logger.LogInformation($"Email enviado exitosamente. ID: {response}");
                    return new EmailResponse
                    {
                        Success = true,
                        Message = "Email enviado exitosamente",
                        EmailId = response.ToString()
                    };
                }
                else
                {
                    _logger.LogError("Error al enviar email: Respuesta nula");
                    return new EmailResponse
                    {
                        Success = false,
                        Error = "Error desconocido al enviar email"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al enviar email");
                return new EmailResponse
                {
                    Success = false,
                    Error = $"Error interno: {ex.Message}"
                };
            }
        }

        public async Task<EmailResponse> SendPaymentConfirmationAsync(PaymentConfirmationEmailRequest request)
        {
            var htmlContent = GeneratePaymentConfirmationHtml(request);
            var textContent = GeneratePaymentConfirmationText(request);

            var emailRequest = new EmailRequest
            {
                To = request.CustomerEmail,
                Subject = $"Confirmación de Pago - Factura #{request.InvoiceNumber}",
                HtmlContent = htmlContent,
                TextContent = textContent,
                Tags = new Dictionary<string, string>
                {
                    { "type", "payment_confirmation" },
                    { "invoice", request.InvoiceNumber },
                    { "transaction_id", request.TransactionId }
                }
            };

            return await SendEmailAsync(emailRequest);
        }

        public async Task<EmailResponse> SendInvoiceReminderAsync(InvoiceReminderEmailRequest request)
        {
            var htmlContent = GenerateInvoiceReminderHtml(request);
            var textContent = GenerateInvoiceReminderText(request);

            var emailRequest = new EmailRequest
            {
                To = request.CustomerEmail,
                Subject = $"Recordatorio de Pago - Factura #{request.InvoiceNumber}",
                HtmlContent = htmlContent,
                TextContent = textContent,
                Tags = new Dictionary<string, string>
                {
                    { "type", "invoice_reminder" },
                    { "invoice", request.InvoiceNumber },
                    { "days_overdue", request.DaysOverdue.ToString() }
                }
            };

            return await SendEmailAsync(emailRequest);
        }

        public async Task<EmailResponse> SendWelcomeEmailAsync(WelcomeEmailRequest request)
        {
            var htmlContent = GenerateWelcomeEmailHtml(request);
            var textContent = GenerateWelcomeEmailText(request);

            var emailRequest = new EmailRequest
            {
                To = request.CustomerEmail,
                Subject = "¡Bienvenido a Celero!",
                HtmlContent = htmlContent,
                TextContent = textContent,
                Tags = new Dictionary<string, string>
                {
                    { "type", "welcome" },
                    { "customer_code", request.CustomerCode ?? "" }
                }
            };

            return await SendEmailAsync(emailRequest);
        }

        private string GeneratePaymentConfirmationHtml(PaymentConfirmationEmailRequest request)
        {
            var logoUrl = request.CompanyCode == "3" 
                ? "https://selfservice-dev.celero.network/lovable-uploads/Logo_Celero_Quantum.svg"
                : "https://selfservice-dev.celero.network/lovable-uploads/Logo_Celero_Networks.svg";
            var companyName = request.CompanyCode == "3" ? "CELERO QUANTUM" : "CELERO NETWORK";
            
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <title>Confirmación de Pago - {companyName}</title>
    <!--[if mso]>
    <noscript>
        <xml>
            <o:OfficeDocumentSettings>
                <o:PixelsPerInch>96</o:PixelsPerInch>
            </o:OfficeDocumentSettings>
        </xml>
    </noscript>
    <![endif]-->
</head>
<body style='margin: 0; padding: 0; font-family: Arial, Helvetica, sans-serif; background-color: #f5f5f5; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%;'>
    <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #f5f5f5;'>
        <tr>
            <td align='center' style='padding: 20px 0;'>
                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='600' style='max-width: 600px; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                    <!-- Header con Logo -->
                    <tr>
                        <td style='background-color: #ffffff; padding: 30px 20px; text-align: center; border-bottom: 3px solid #667eea; border-radius: 8px 8px 0 0;'>
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td align='center'>
                                        <img src='{logoUrl}' 
                                             alt='{companyName} - Sistema de Pagos Seguro' 
                                             title='{companyName}' 
                                             width='250' 
                                             height='auto' 
                                             style='display: block; max-width: 250px; height: auto; margin: 0 auto; border: 0; outline: none; text-decoration: none;' />
                                        <div style='font-size: 26px; font-weight: bold; color: #667eea; margin: 15px 0 5px 0; line-height: 1.2; mso-hide: all;'>
                                            🌐 {companyName}
                                        </div>
                                        <div style='font-size: 14px; color: #666; margin: 0; mso-hide: all;'>
                                            Sistema de Pagos Seguro
                                        </div>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Título principal -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px 20px; text-align: center;'>
                            <h1 style='color: white; margin: 0; font-size: 28px; font-weight: bold; line-height: 1.2;'>✅ Pago Confirmado</h1>
                            <p style='color: #f8f9fa; margin: 10px 0 0 0; font-size: 16px; line-height: 1.4;'>¡Gracias por su pago!</p>
                        </td>
                    </tr>
                    
                    <!-- Contenido -->
                    <tr>
                        <td style='padding: 30px 20px; background-color: #f8f9fa;'>
                            <h2 style='color: #333; font-size: 22px; margin: 0 0 20px 0;'>Estimado/a {request.CustomerName},</h2>
                            <p style='color: #333; font-size: 16px; margin: 0 0 20px 0;'>Su pago ha sido procesado exitosamente.</p>
                            
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #ffffff; border-radius: 6px; margin-bottom: 20px;'>
                                <tr>
                                    <td style='padding: 20px;'>
                                        <h3 style='color: #667eea; font-size: 18px; margin: 0 0 15px 0;'>Detalles del Pago:</h3>
                                        <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                            <tr>
                                                <td style='padding: 8px 0; color: #666;'><strong>Factura:</strong></td>
                                                <td style='padding: 8px 0; color: #333; text-align: right;'>#{request.InvoiceNumber}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding: 8px 0; color: #666;'><strong>Monto:</strong></td>
                                                <td style='padding: 8px 0; text-align: right;'><span style='font-size: 24px; font-weight: bold; color: #28a745;'>${request.Amount:F2}</span></td>
                                            </tr>
                                            <tr>
                                                <td style='padding: 8px 0; color: #666;'><strong>Método de Pago:</strong></td>
                                                <td style='padding: 8px 0; color: #333; text-align: right;'>{request.PaymentMethod}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding: 8px 0; color: #666;'><strong>ID de Transacción:</strong></td>
                                                <td style='padding: 8px 0; color: #333; text-align: right;'>{request.TransactionId}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding: 8px 0; color: #666;'><strong>Fecha de Pago:</strong></td>
                                                <td style='padding: 8px 0; color: #333; text-align: right;'>{request.PaymentDate:dd/MM/yyyy HH:mm}</td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                            <p style='color: #333; font-size: 16px; margin: 0 0 20px 0;'>Gracias por su pago. Si tiene alguna pregunta, no dude en contactarnos.</p>
                            
                            <!-- Botón WhatsApp -->
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td align='center' style='padding: 20px 0;'>
                                        <a href='https://api.whatsapp.com/send/?phone=50768587775' 
                                           style='display: inline-block; background-color: #25D366; color: white; padding: 12px 30px; text-decoration: none; border-radius: 25px; font-weight: bold; font-size: 16px;'>
                                            📱 Clic aquí para contactarnos por WhatsApp
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='text-align: center; padding: 30px 20px; background-color: #f8f9fa; border-radius: 0 0 8px 8px;'>
                            <p style='color: #6c757d; font-size: 12px; margin: 0; line-height: 1.4;'>
                                © 2025 {companyName} - Sistema de Pagos Seguro
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private string GeneratePaymentConfirmationText(PaymentConfirmationEmailRequest request)
        {
            return $@"
Confirmación de Pago

Estimado/a {request.CustomerName},

Su pago ha sido procesado exitosamente.

Detalles del Pago:
- Factura: #{request.InvoiceNumber}
- Monto: ${request.Amount:F2}
- Método de Pago: {request.PaymentMethod}
- ID de Transacción: {request.TransactionId}
- Fecha de Pago: {request.PaymentDate:dd/MM/yyyy HH:mm}

Gracias por su pago. Si tiene alguna pregunta, no dude en contactarnos.

Clic aquí para contactarnos por WhatsApp: https://api.whatsapp.com/send/?phone=50768587775

© 2025 Celero. Todos los derechos reservados.
            ";
        }

        private string GenerateInvoiceReminderHtml(InvoiceReminderEmailRequest request)
        {
            var urgencyClass = request.DaysOverdue > 30 ? "urgent" : request.DaysOverdue > 0 ? "warning" : "info";
            var urgencyColor = request.DaysOverdue > 30 ? "#dc3545" : request.DaysOverdue > 0 ? "#ffc107" : "#17a2b8";
            var urgencyIcon = request.DaysOverdue > 30 ? "⚠️" : request.DaysOverdue > 0 ? "⏰" : "📋";
            var urgencyText = request.DaysOverdue > 30 ? "Pago Vencido - Urgente" : request.DaysOverdue > 0 ? "Pago Vencido" : "Recordatorio de Pago";
            var logoUrl = request.CompanyCode == "3" 
                ? "https://selfservice-dev.celero.network/lovable-uploads/Logo_Celero_Quantum.svg"
                : "https://selfservice-dev.celero.network/lovable-uploads/Logo_Celero_Networks.svg";
            var companyName = request.CompanyCode == "3" ? "CELERO QUANTUM" : "CELERO NETWORK";

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <title>Recordatorio de Pago - {companyName}</title>
    <!--[if mso]>
    <noscript>
        <xml>
            <o:OfficeDocumentSettings>
                <o:PixelsPerInch>96</o:PixelsPerInch>
            </o:OfficeDocumentSettings>
        </xml>
    </noscript>
    <![endif]-->
</head>
<body style='margin: 0; padding: 0; font-family: Arial, Helvetica, sans-serif; background-color: #f5f5f5; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%;'>
    <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #f5f5f5;'>
        <tr>
            <td align='center' style='padding: 20px 0;'>
                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='600' style='max-width: 600px; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                    <!-- Header con Logo -->
                    <tr>
                        <td style='background-color: #ffffff; padding: 30px 20px; text-align: center; border-bottom: 3px solid {urgencyColor}; border-radius: 8px 8px 0 0;'>
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td align='center'>
                                        <img src='{logoUrl}' 
                                             alt='{companyName} - Sistema de Pagos Seguro' 
                                             title='{companyName}' 
                                             width='250' 
                                             height='auto' 
                                             style='display: block; max-width: 250px; height: auto; margin: 0 auto; border: 0; outline: none; text-decoration: none;' />
                                        <div style='font-size: 26px; font-weight: bold; color: {urgencyColor}; margin: 15px 0 5px 0; line-height: 1.2; mso-hide: all;'>
                                            🌐 {companyName}
                                        </div>
                                        <div style='font-size: 14px; color: #666; margin: 0; mso-hide: all;'>
                                            Sistema de Pagos Seguro
                                        </div>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Título principal -->
                    <tr>
                        <td style='background-color: {urgencyColor}; padding: 30px 20px; text-align: center;'>
                            <h1 style='color: white; margin: 0; font-size: 28px; font-weight: bold; line-height: 1.2;'>{urgencyIcon} {urgencyText}</h1>
                            <p style='color: #f8f9fa; margin: 10px 0 0 0; font-size: 16px; line-height: 1.4;'>Factura pendiente de pago</p>
                        </td>
                    </tr>
                    
                    <!-- Contenido -->
                    <tr>
                        <td style='padding: 30px 20px; background-color: #f8f9fa;'>
                            <h2 style='color: #333; font-size: 22px; margin: 0 0 20px 0;'>Estimado/a {request.CustomerName},</h2>
                            <p style='color: #333; font-size: 16px; margin: 0 0 20px 0;'>Le recordamos que tiene una factura pendiente de pago.</p>
                            
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #ffffff; border-radius: 6px; margin-bottom: 20px; border-left: 4px solid {urgencyColor};'>
                                <tr>
                                    <td style='padding: 20px;'>
                                        <h3 style='color: {urgencyColor}; font-size: 18px; margin: 0 0 15px 0;'>Detalles de la Factura:</h3>
                                        <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                            <tr>
                                                <td style='padding: 8px 0; color: #666;'><strong>Factura:</strong></td>
                                                <td style='padding: 8px 0; color: #333; text-align: right;'>#{request.InvoiceNumber}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding: 8px 0; color: #666;'><strong>Monto:</strong></td>
                                                <td style='padding: 8px 0; text-align: right;'><span style='font-size: 24px; font-weight: bold; color: {urgencyColor};'>${request.Amount:F2}</span></td>
                                            </tr>
                                            <tr>
                                                <td style='padding: 8px 0; color: #666;'><strong>Fecha de Vencimiento:</strong></td>
                                                <td style='padding: 8px 0; color: #333; text-align: right;'>{request.DueDate:dd/MM/yyyy}</td>
                                            </tr>
                                            {(request.DaysOverdue > 0 ? $@"<tr>
                                                <td style='padding: 8px 0; color: #666;'><strong>Días de Atraso:</strong></td>
                                                <td style='padding: 8px 0; text-align: right;'><span style='color: #dc3545; font-weight: bold;'>{request.DaysOverdue} días</span></td>
                                            </tr>" : "")}
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                            <p style='color: #333; font-size: 16px; margin: 0 0 20px 0;'>Por favor, proceda con el pago a la brevedad posible. Si ya realizó el pago, ignore este mensaje.</p>
                            
                            <!-- Botón WhatsApp -->
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td align='center' style='padding: 20px 0;'>
                                        <a href='https://api.whatsapp.com/send/?phone=50768587775' 
                                           style='display: inline-block; background-color: #25D366; color: white; padding: 12px 30px; text-decoration: none; border-radius: 25px; font-weight: bold; font-size: 16px;'>
                                            📱 Clic aquí para contactarnos por WhatsApp
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='text-align: center; padding: 30px 20px; background-color: #f8f9fa; border-radius: 0 0 8px 8px;'>
                            <p style='color: #6c757d; font-size: 12px; margin: 0; line-height: 1.4;'>
                                © 2025 {companyName} - Sistema de Pagos Seguro
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private string GenerateInvoiceReminderText(InvoiceReminderEmailRequest request)
        {
            return $@"
Recordatorio de Pago

Estimado/a {request.CustomerName},

Le recordamos que tiene una factura pendiente de pago.

Detalles de la Factura:
- Factura: #{request.InvoiceNumber}
- Monto: ${request.Amount:F2}
- Fecha de Vencimiento: {request.DueDate:dd/MM/yyyy}
{(request.DaysOverdue > 0 ? $"- Días de Atraso: {request.DaysOverdue} días" : "")}

Por favor, proceda con el pago a la brevedad posible. Si ya realizó el pago, ignore este mensaje.

Clic aquí para contactarnos por WhatsApp: https://api.whatsapp.com/send/?phone=50768587775

© 2025 Celero. Todos los derechos reservados.
            ";
        }

        private string GenerateWelcomeEmailHtml(WelcomeEmailRequest request)
        {
            var logoUrl = request.CompanyCode == "3" 
                ? "https://selfservice-dev.celero.network/lovable-uploads/Logo_Celero_Quantum.svg"
                : "https://selfservice-dev.celero.network/lovable-uploads/Logo_Celero_Networks.svg";
            var companyName = request.CompanyCode == "3" ? "CELERO QUANTUM" : "CELERO NETWORK";
            
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <title>Bienvenido a {companyName}</title>
    <!--[if mso]>
    <noscript>
        <xml>
            <o:OfficeDocumentSettings>
                <o:PixelsPerInch>96</o:PixelsPerInch>
            </o:OfficeDocumentSettings>
        </xml>
    </noscript>
    <![endif]-->
</head>
<body style='margin: 0; padding: 0; font-family: Arial, Helvetica, sans-serif; background-color: #f5f5f5; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%;'>
    <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #f5f5f5;'>
        <tr>
            <td align='center' style='padding: 20px 0;'>
                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='600' style='max-width: 600px; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                    <!-- Header con Logo -->
                    <tr>
                        <td style='background-color: #ffffff; padding: 30px 20px; text-align: center; border-bottom: 3px solid #28a745; border-radius: 8px 8px 0 0;'>
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td align='center'>
                                        <img src='{logoUrl}' 
                                             alt='{companyName} - Sistema de Pagos Seguro' 
                                             title='{companyName}' 
                                             width='250' 
                                             height='auto' 
                                             style='display: block; max-width: 250px; height: auto; margin: 0 auto; border: 0; outline: none; text-decoration: none;' />
                                        <div style='font-size: 26px; font-weight: bold; color: #28a745; margin: 15px 0 5px 0; line-height: 1.2; mso-hide: all;'>
                                            🌐 {companyName}
                                        </div>
                                        <div style='font-size: 14px; color: #666; margin: 0; mso-hide: all;'>
                                            Sistema de Pagos Seguro
                                        </div>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Título principal -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #28a745 0%, #20c997 100%); padding: 30px 20px; text-align: center;'>
                            <h1 style='color: white; margin: 0; font-size: 28px; font-weight: bold; line-height: 1.2;'>🎉 ¡Bienvenido a Celero!</h1>
                            <p style='color: #f8f9fa; margin: 10px 0 0 0; font-size: 16px; line-height: 1.4;'>Su cuenta ha sido creada exitosamente</p>
                        </td>
                    </tr>
                    
                    <!-- Contenido -->
                    <tr>
                        <td style='padding: 30px 20px; background-color: #f8f9fa;'>
                            <h2 style='color: #333; font-size: 22px; margin: 0 0 20px 0;'>Estimado/a {request.CustomerName},</h2>
                            <p style='color: #333; font-size: 16px; margin: 0 0 20px 0;'>¡Nos complace darle la bienvenida a Celero!</p>
                            
                            {(!string.IsNullOrEmpty(request.CustomerCode) ? $@"
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #ffffff; border-radius: 6px; margin-bottom: 20px; border-left: 4px solid #28a745;'>
                                <tr>
                                    <td style='padding: 20px; text-align: center;'>
                                        <p style='color: #666; font-size: 14px; margin: 0 0 5px 0;'>Su código de cliente es:</p>
                                        <p style='color: #28a745; font-size: 28px; font-weight: bold; margin: 0;'>{request.CustomerCode}</p>
                                    </td>
                                </tr>
                            </table>" : "")}
                            
                            <p style='color: #333; font-size: 16px; margin: 0 0 10px 0;'>Ahora puede acceder a nuestros servicios y gestionar sus facturas de manera fácil y segura.</p>
                            
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #e8f5e9; border-radius: 6px; margin: 20px 0;'>
                                <tr>
                                    <td style='padding: 20px;'>
                                        <h3 style='color: #28a745; font-size: 18px; margin: 0 0 10px 0;'>Con su cuenta puede:</h3>
                                        <ul style='color: #333; font-size: 14px; margin: 0; padding-left: 20px;'>
                                            <li style='margin-bottom: 8px;'>Consultar sus facturas en línea</li>
                                            <li style='margin-bottom: 8px;'>Realizar pagos de manera segura</li>
                                            <li style='margin-bottom: 8px;'>Ver el historial de transacciones</li>
                                            <li style='margin-bottom: 8px;'>Descargar comprobantes de pago</li>
                                        </ul>
                                    </td>
                                </tr>
                            </table>
                            
                            <p style='color: #333; font-size: 16px; margin: 20px 0;'>Si tiene alguna pregunta o necesita asistencia, no dude en contactarnos.</p>
                            
                            <!-- Botón WhatsApp -->
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td align='center' style='padding: 20px 0;'>
                                        <a href='https://api.whatsapp.com/send/?phone=50768587775' 
                                           style='display: inline-block; background-color: #25D366; color: white; padding: 12px 30px; text-decoration: none; border-radius: 25px; font-weight: bold; font-size: 16px;'>
                                            📱 Clic aquí para contactarnos por WhatsApp
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='text-align: center; padding: 30px 20px; background-color: #f8f9fa; border-radius: 0 0 8px 8px;'>
                            <p style='color: #6c757d; font-size: 12px; margin: 0; line-height: 1.4;'>
                                © 2025 {companyName} - Sistema de Pagos Seguro
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private string GenerateWelcomeEmailText(WelcomeEmailRequest request)
        {
            return $@"
¡Bienvenido a Celero!

Estimado/a {request.CustomerName},

¡Nos complace darle la bienvenida a Celero!

{(!string.IsNullOrEmpty(request.CustomerCode) ? $"Su código de cliente es: {request.CustomerCode}" : "")}

Ahora puede acceder a nuestros servicios y gestionar sus facturas de manera fácil y segura.

Si tiene alguna pregunta o necesita asistencia, no dude en contactarnos.

Clic aquí para contactarnos por WhatsApp: https://api.whatsapp.com/send/?phone=50768587775

© 2025 Celero. Todos los derechos reservados.
            ";
        }

        public async Task<EmailResponse> SendReceiptEmailAsync(ReceiptEmailRequest request)
        {
            var htmlContent = GenerateReceiptEmailHtml(request);
            var textContent = GenerateReceiptEmailText(request);

            var emailRequest = new EmailRequest
            {
                To = request.CustomerEmail,
                From = "noreply@celero.net",
                Subject = $"Confirmación de Pago - Recibo #{request.ReceiptNumber}",
                HtmlContent = htmlContent,
                TextContent = textContent,
                Headers = new Dictionary<string, string>
                {
                    { "X-Priority", "3" },
                    { "X-Mailer", "Celero Payment System" },
                    { "List-Unsubscribe", "<mailto:unsubscribe@celero.net>" }
                },
                Tags = new Dictionary<string, string>
                {
                    { "type", "receipt_notification" },
                    { "receipt", request.ReceiptNumber },
                    { "company", request.CompanyCode }
                }
            };

            return await SendEmailAsync(emailRequest);
        }

        private string GenerateReceiptEmailHtml(ReceiptEmailRequest request)
        {
            // Determinar el logo según la empresa
            var logoUrl = request.CompanyCode == "3" 
                ? "https://i.imgur.com/QcHzleK.png"  // Empresa 3 - Celero Quantum
                : "https://i.imgur.com/DYW7gJG.png"; // Empresa 2 - Celero Networks

            // Generar las filas de la tabla de detalles
            var detallesHtml = "";
            if (request.Details != null && request.Details.Count > 0)
            {
                foreach (var detalle in request.Details)
                {
                    detallesHtml += $@"
                                        <tr>
                                            <td style='padding: 8px; border: none; text-align: left; color: #333; font-size: 11px;'>{detalle.Reference}</td>
                                            <td style='padding: 8px; border: none; text-align: left; color: #333; font-size: 11px;'>{detalle.CufeNumber}</td>
                                            <td style='padding: 8px; border: none; text-align: center; color: #333; font-size: 11px;'>{detalle.Quota}</td>
                                            <td style='padding: 8px; border: none; text-align: right; color: #333; font-size: 11px;'>${detalle.ReceivedAmount:F2}</td>
                                        </tr>";
                }
            }

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Confirmación de Pago - Celero Network</title>
</head>
<body style='margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #ffffff;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #ffffff; padding: 20px 0;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background-color: #ffffff;'>
                    <!-- Header con logo -->
                    <tr>
                        <td style='padding: 20px; text-align: left;'>
                            <img src='{logoUrl}' 
                                 alt='Celero Network Logo' 
                                 width='150' 
                                 style='display: block; height: auto; margin-left: 17px;' />
                        </td>
                    </tr>
                    
                    <!-- Título principal -->
                    <tr>
                        <td style='padding: 30px 40px 20px 40px; text-align: left;'>
                            <h1 style='color: #333; font-size: 24px; font-weight: bold; margin: 0;'>¡Gracias por su pago!</h1>
                        </td>
                    </tr>
                    
                    <!-- Información del cliente y pago -->
                    <tr>
                        <td style='padding: 0 40px 20px 40px;'>
                            <p style='margin: 0 0 10px 0; font-size: 16px; color: #333;'>
                                Estimado. <strong style='color: #6c5ce7;'>{request.CustomerName}</strong>
                            </p>
                            <p style='margin: 0; font-size: 16px; color: #333;'>
                                Hemos aplicado su cuenta <strong>${request.TotalAmount:F2}</strong> recibido el <strong>{request.TransactionDate}</strong> mediante <strong>{request.PaymentMethod}</strong>.
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Tabla de detalle de cobro -->
                    <tr>
                        <td style='padding: 0 40px 30px 40px;'>
                            <div style='border: 2px solid #6c5ce7; padding: 20px; border-radius: 4px;'>
                                <h3 style='color: #6c5ce7; margin: 0 0 15px 0; font-size: 18px; font-weight: bold;'>Detalle de Cobro</h3>
                                
                                <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse: collapse; font-size: 12px;'>
                                    <thead>
                                        <tr>
                                            <th style='padding: 8px; border: none; border-bottom: 1px solid #6c5ce7; text-align: left; font-weight: bold; color: #333; font-size: 11px;'>REFERENCIA</th>
                                            <th style='padding: 8px; border: none; border-bottom: 1px solid #6c5ce7; text-align: left; font-weight: bold; color: #333; font-size: 11px;'>Nro. CUFE</th>
                                            <th style='padding: 8px; border: none; border-bottom: 1px solid #6c5ce7; text-align: center; font-weight: bold; color: #333; font-size: 11px;'>CUOTA</th>
                                            <th style='padding: 8px; border: none; border-bottom: 1px solid #6c5ce7; text-align: right; font-weight: bold; color: #333; font-size: 11px;'>RECIBIDO</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {detallesHtml}
                                    </tbody>
                                </table>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Información de contacto -->
                    <tr>
                        <td style='padding: 0 40px 20px 40px;'>
                            <p style='margin: 0 0 5px 0; font-size: 14px; color: #333;'>
                                Si mantiene alguna consulta sobre la facturación, no dude en contactarnos respondiendo a esta notificación, llamando al <strong>+507 838-7575</strong> o a nuestro
                            </p>
                            <p style='margin: 0; font-size: 14px; color: #333;'>
                                <strong>Whatsapp</strong> haciendo <a href='https://api.whatsapp.com/send/?phone=50768587775' style='color: #6c5ce7; text-decoration: none;'><strong>clic aquí</strong></a>.
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Firma -->
                    <tr>
                        <td style='padding: 0 40px 30px 40px;'>
                            <p style='margin: 0; font-size: 14px; color: #333;'>Atentamente,</p>
                            <p style='margin: 5px 0 0 0; font-size: 14px; color: #333; font-weight: bold;'>Depto. Crédito y Cobros</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private string GenerateReceiptEmailText(ReceiptEmailRequest request)
        {
            var detallesText = "";
            if (request.Details != null && request.Details.Count > 0)
            {
                foreach (var detalle in request.Details)
                {
                    detallesText += $@"
  - Referencia: {detalle.Reference}
    Nro. CUFE: {detalle.CufeNumber}
    Cuota: {detalle.Quota}
    Recibido: ${detalle.ReceivedAmount:F2}";
                }
            }

            return $@"¡Gracias por su pago!

Estimado {request.CustomerName},

Hemos aplicado su cuenta ${request.TotalAmount:F2} recibido el {request.TransactionDate} mediante {request.PaymentMethod}.

Detalle de Cobro:
{detallesText}

Si mantiene alguna consulta sobre la facturación, no dude en contactarnos respondiendo a esta notificación, llamando al +507 838-7575 o a nuestro Whatsapp haciendo clic aquí: https://api.whatsapp.com/send/?phone=50768587775

Atentamente,
Depto. Crédito y Cobros";
        }
    }
}
