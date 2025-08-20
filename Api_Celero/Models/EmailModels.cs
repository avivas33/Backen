using System.ComponentModel.DataAnnotations;

namespace Api_Celero.Models
{
    // Modelo para la solicitud de envío de email desde el frontend
    public class EmailRequest
    {
        [Required]
        public string To { get; set; } = string.Empty;
        public string? ToName { get; set; }
        [Required]
        public string Subject { get; set; } = string.Empty;
        public string? TextContent { get; set; }
        public string? HtmlContent { get; set; }

        public string? From { get; set; } // Opcional, se puede usar el default del config
        public string? FromName { get; set; } // Opcional
        public List<EmailAttachment>? Attachments { get; set; }
        public Dictionary<string, string>? Tags { get; set; } // Para tracking

        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(); // NUEVA PROPIEDAD
    }

    public class EmailAttachment
    {
        [Required]
        public string Filename { get; set; } = string.Empty;
        [Required]
        public string ContentType { get; set; } = string.Empty;
        [Required]
        public string Content { get; set; } = string.Empty; // Base64 encoded
    }

    // Modelo para respuesta del envío de email
    public class EmailResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? EmailId { get; set; } // ID del email enviado por Resend
        public string? Error { get; set; }
    }

    // Modelos específicos para diferentes tipos de emails
    public class PaymentConfirmationEmailRequest
    {
        [Required]
        [EmailAddress]
        public string CustomerEmail { get; set; } = string.Empty;
        [Required]
        public string CustomerName { get; set; } = string.Empty;
        [Required]
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        [Required]
        public string PaymentMethod { get; set; } = string.Empty;
        [Required]
        public string TransactionId { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public string? CompanyCode { get; set; }
    }

    public class InvoiceReminderEmailRequest
    {
        [Required]
        [EmailAddress]
        public string CustomerEmail { get; set; } = string.Empty;
        [Required]
        public string CustomerName { get; set; } = string.Empty;
        [Required]
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public int DaysOverdue { get; set; }
        public string? CompanyCode { get; set; }
    }

    public class WelcomeEmailRequest
    {
        [Required]
        [EmailAddress]
        public string CustomerEmail { get; set; } = string.Empty;
        [Required]
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerCode { get; set; }
        public string? CompanyCode { get; set; }
    }
}
