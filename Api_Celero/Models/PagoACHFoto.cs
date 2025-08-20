using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_Celero.Models
{
    [Table("PagoACHFotos")]
    public class PagoACHFoto
    {
        [Key]
        public int Id { get; set; }
        
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
        public byte[] FotoComprobante { get; set; } = Array.Empty<byte>();
        
        [Required]
        [StringLength(100)]
        public string NombreArchivo { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string TipoArchivo { get; set; } = string.Empty;
        
        [Required]
        public long TamanoArchivo { get; set; }
        
        [Required]
        public decimal MontoTransaccion { get; set; }
        
        [Required]
        public DateTime FechaTransaccion { get; set; }
        
        [Required]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
        
        [StringLength(500)]
        public string? Observaciones { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "PENDIENTE"; // PENDIENTE, PROCESADO, RECHAZADO
        
        [StringLength(100)]
        public string? UsuarioRegistro { get; set; }
        
        public DateTime? FechaProcesamiento { get; set; }
        
        [StringLength(500)]
        public string? MotivoRechazo { get; set; }
    }
}