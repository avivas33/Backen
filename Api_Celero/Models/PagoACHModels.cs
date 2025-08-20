using System.ComponentModel.DataAnnotations;

namespace Api_Celero.Models
{
    public class PagoACHRequest
    {
        [Required]
        [StringLength(50)]
        public string ClienteCode { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string NumeroFactura { get; set; } = string.Empty;
        
        [Required]
        [StringLength(10)]
        public string EmpresaCode { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string NumeroTransaccion { get; set; } = string.Empty;
        
        [Required]
        public IFormFile FotoComprobante { get; set; } = null!;
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
        public decimal MontoTransaccion { get; set; }
        
        [Required]
        public DateTime FechaTransaccion { get; set; }
        
        [StringLength(500)]
        public string? Observaciones { get; set; }
        
        [StringLength(100)]
        public string? UsuarioRegistro { get; set; }
    }
    
    public class PagoACHResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? PagoId { get; set; }
        public DateTime FechaRegistro { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
    
    public class PagoACHConsultaResponse
    {
        public int Id { get; set; }
        public string ClienteCode { get; set; } = string.Empty;
        public string NumeroFactura { get; set; } = string.Empty;
        public string EmpresaCode { get; set; } = string.Empty;
        public string NumeroTransaccion { get; set; } = string.Empty;
        public string NombreArchivo { get; set; } = string.Empty;
        public string TipoArchivo { get; set; } = string.Empty;
        public long TamanoArchivo { get; set; }
        public decimal MontoTransaccion { get; set; }
        public DateTime FechaTransaccion { get; set; }
        public DateTime FechaRegistro { get; set; }
        public string? Observaciones { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? UsuarioRegistro { get; set; }
        public DateTime? FechaProcesamiento { get; set; }
        public string? MotivoRechazo { get; set; }
    }
    
    public class PagoACHFotoResponse
    {
        public byte[] FotoComprobante { get; set; } = Array.Empty<byte>();
        public string TipoArchivo { get; set; } = string.Empty;
        public string NombreArchivo { get; set; } = string.Empty;
    }
}