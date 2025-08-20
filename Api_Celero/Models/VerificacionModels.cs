namespace Api_Celero.Models
{
    public class SolicitudCodigoVerificacion
    {
        public string Email { get; set; } = string.Empty;
        public string ClienteCode { get; set; } = string.Empty;
        public string TipoConsulta { get; set; } = string.Empty; // "facturas" o "recibos"
    }

    public class VerificarCodigoRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string ClienteCode { get; set; } = string.Empty;
        public string TipoConsulta { get; set; } = string.Empty;
    }

    public class CodigoVerificacion
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string ClienteCode { get; set; } = string.Empty;
        public string TipoConsulta { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaExpiracion { get; set; }
        public bool Usado { get; set; } = false;
    }
}