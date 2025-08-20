using System;
using System.ComponentModel.DataAnnotations;

namespace Api_Celero.Models
{
    public class ActivityLog
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string EventType { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? Fingerprint { get; set; }
        
        [MaxLength(45)]
        public string? IpAddress { get; set; }
        
        [MaxLength(500)]
        public string? UserAgent { get; set; }
        
        [MaxLength(50)]
        public string? TimeZone { get; set; }
        
        [MaxLength(20)]
        public string? ScreenResolution { get; set; }
        
        [MaxLength(10)]
        public string? BrowserLanguage { get; set; }
        
        [MaxLength(100)]
        public string? ClientId { get; set; }
        
        [MaxLength(100)]
        public string? SessionId { get; set; }
        
        public string? AdditionalData { get; set; } // JSON para datos extra
        
        public DateTime CreatedAtUtc { get; set; }
        
        // Datos opcionales del pago
        [MaxLength(50)]
        public string? PaymentMethod { get; set; }
        
        public decimal? Amount { get; set; }
        
        [MaxLength(10)]
        public string? Currency { get; set; }
        
        [MaxLength(50)]
        public string? PaymentStatus { get; set; }
        
        [MaxLength(100)]
        public string? ErrorCode { get; set; }
    }

    public class RequestLog
    {
        [Key]
        public long Id { get; set; }
        
        [Required]
        [MaxLength(45)]
        public string IpAddress { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? UserAgent { get; set; }
        
        [Required]
        [MaxLength(10)]
        public string Method { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(500)]
        public string Path { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? QueryString { get; set; }
        
        public int? StatusCode { get; set; }
        
        public long? ResponseTimeMs { get; set; }
        
        public DateTime RequestTimeUtc { get; set; }
        
        [MaxLength(100)]
        public string? ClientId { get; set; }
    }
}