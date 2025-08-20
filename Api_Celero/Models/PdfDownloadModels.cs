
namespace Api_Celero.Models
{
    /// <summary>
    /// Modelo para el request de descarga de PDF por número de factura
    /// </summary>
    public class PdfDownloadRequest
    {
        /// <summary>
        /// Número de factura para descargar el PDF
        /// Ejemplo: "FE0120000155747009-2-2024-8000012025062500000007780010317838356743"
        /// </summary>
        public string NoFactura { get; set; } = string.Empty;
    }

    /// <summary>
    /// Modelo para la respuesta del endpoint de descarga de PDF
    /// </summary>
    public class PdfDownloadResponse
    {
        /// <summary>
        /// Indica si la descarga fue exitosa
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Mensaje descriptivo del resultado
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Nombre del archivo PDF generado (solo si Success = true)
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Tamaño del archivo en bytes (solo si Success = true)
        /// </summary>
        public long? FileSize { get; set; }

        /// <summary>
        /// URL o ruta para descargar el archivo (si aplica)
        /// </summary>
        public string? DownloadUrl { get; set; }
    }
}
