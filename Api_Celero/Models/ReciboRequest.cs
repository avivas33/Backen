using System.ComponentModel.DataAnnotations;

namespace Api_Celero.Models
{
    public class ReciboRequest
    {
        
        public string SerNr { get; set; } = string.Empty;
        
        [Required]
        public string TransDate { get; set; } = string.Empty;
        
        [Required]
        public string PayMode { get; set; } = string.Empty;
        
    
        public string Person { get; set; } = string.Empty;
        
        [Required]
        public string CUCode { get; set; } = string.Empty;
        
        [Required]
        public string RefStr { get; set; } = string.Empty;
        
        // Campo opcional para el email del cliente (para notificaciones)
        public string? Email { get; set; }
        
        [Required]
        public List<DetalleRequest> Detalles { get; set; } = new List<DetalleRequest>();
    }

    public class DetalleRequest
    {
        [Required]
        public string InvoiceNr { get; set; } = string.Empty;
        
        [Required]
        public decimal Sum { get; set; }
        
        public string Objects { get; set; } = string.Empty;
        
        
        public string Stp { get; set; } = string.Empty;
    }

    public class ReciboResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? SerNr { get; set; }
    }
}