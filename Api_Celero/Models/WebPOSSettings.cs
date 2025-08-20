
namespace Api_Celero.Models
{
    /// <summary>
    /// Configuración para la API WebPOS FEPA
    /// </summary>
    public class WebPOSSettings
    {
        /// <summary>
        /// URL base de la API WebPOS
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Código de empresa para WebPOS
        /// </summary>
        public string Empresa { get; set; } = string.Empty;

        /// <summary>
        /// GUID de autenticación para WebPOS
        /// </summary>
        public string Guid { get; set; } = string.Empty;
    }
}