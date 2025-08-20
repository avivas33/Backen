
using System.Text.Json;
using Microsoft.Extensions.Options;
using Api_Celero.Models;

namespace Api_Celero.Services
{
    /// <summary>
    /// Servicio para descargar PDFs desde la API WebPOS FEPA
    /// </summary>
    public class PdfDownloaderService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PdfDownloaderService> _logger;
        private readonly WebPOSSettings _webPOSSettings;

        public PdfDownloaderService(
            IHttpClientFactory httpClientFactory, 
            ILogger<PdfDownloaderService> logger,
            IOptions<WebPOSSettings> webPOSSettings)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _webPOSSettings = webPOSSettings.Value;
        }

        /// <summary>
        /// Descarga un PDF desde la API WebPOS FEPA que retorna el contenido en base64
        /// Versión mejorada con manejo completo de la respuesta JSON estructurada
        /// </summary>
        /// <param name="url">URL completa de la API GetPdf de WebPOS</param>
        /// <returns>Contenido del PDF en bytes o null si hay error</returns>
        public async Task<byte[]?> DownloadPdfFromApiAsync(string url)
        {
            try
            {
                var result = await DownloadPdfWithDetailsAsync(url);
                return result.Success ? result.PdfData : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar el PDF desde: {Url}", url);
                return null;
            }
        }

        /// <summary>
        /// Descarga un PDF con información detallada de la respuesta
        /// </summary>
        /// <param name="url">URL completa de la API GetPdf de WebPOS</param>
        /// <returns>Resultado completo con datos del PDF y metadata</returns>
        public async Task<PdfExtractionResult> DownloadPdfWithDetailsAsync(string url)
        {
            var result = new PdfExtractionResult();
            
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                
                // Configurar headers
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                
                _logger.LogInformation("Descargando PDF desde: {Url}", url);
                
                // Realizar la solicitud GET a la API
                HttpResponseMessage response = await httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = $"Error en la solicitud HTTP: {response.StatusCode}";
                    _logger.LogError(errorMsg);
                    result.ErrorMessage = errorMsg;
                    return result;
                }

                // Leer el contenido de la respuesta
                string jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Respuesta JSON recibida: {Json}", jsonResponse);
                
                // Procesar la respuesta JSON estructurada
                result = await ProcessWebPOSResponseAsync(jsonResponse);
                
                if (result.Success && result.PdfData != null)
                {
                    _logger.LogInformation("PDF descargado exitosamente. Tamaño: {Size} bytes, Archivo: {FileName}, PDF Generado: {Generated}", 
                        result.PdfData.Length, result.ExtractedFileName, result.PdfWasGenerated);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar el PDF desde: {Url}", url);
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Procesa la respuesta JSON de WebPOS y extrae toda la información disponible
        /// </summary>
        /// <param name="jsonResponse">Respuesta JSON de la API WebPOS</param>
        /// <returns>Resultado del procesamiento con PDF y metadata</returns>
        private async Task<PdfExtractionResult> ProcessWebPOSResponseAsync(string jsonResponse)
        {
            var result = new PdfExtractionResult();
            
            try
            {
                // Intentar deserializar la respuesta completa usando el modelo tipado
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var webPosResponse = JsonSerializer.Deserialize<WebPOSPdfResponse>(jsonResponse, options);
                
                if (webPosResponse != null)
                {
                    result.ResponseData = webPosResponse;
                    result.PdfWasGenerated = webPosResponse.PdfGenerated;
                    result.ExtractedFileName = webPosResponse.FileName;
                    
                    _logger.LogInformation("Respuesta WebPOS procesada - PDF Generado: {Generated}, CUFE: {Cufe}, Archivo: {FileName}", 
                        webPosResponse.PdfGenerated, webPosResponse.Cufe, webPosResponse.FileName);
                    
                    // Verificar si el PDF fue generado exitosamente
                    if (!webPosResponse.PdfGenerated)
                    {
                        result.ErrorMessage = "El PDF no fue generado por WebPOS (pdfGenerated = false)";
                        _logger.LogWarning("WebPOS reportó que el PDF no fue generado");
                        return result;
                    }
                    
                    // Validar que el campo PDF tenga contenido
                    if (string.IsNullOrWhiteSpace(webPosResponse.Pdf))
                    {
                        result.ErrorMessage = "El campo 'pdf' está vacío en la respuesta de WebPOS";
                        _logger.LogError("Campo PDF vacío en respuesta de WebPOS");
                        return result;
                    }
                    
                    // Validar que el contenido base64 sea válido
                    if (!IsValidBase64(webPosResponse.Pdf))
                    {
                        result.ErrorMessage = "El contenido del campo 'pdf' no es un base64 válido";
                        _logger.LogError("Contenido base64 inválido en campo PDF");
                        return result;
                    }
                    
                    // Convertir base64 a bytes
                    result.PdfData = Convert.FromBase64String(webPosResponse.Pdf);
                    result.Success = true;
                    
                    return result;
                }
                else
                {
                    _logger.LogWarning("No se pudo deserializar la respuesta usando el modelo tipado, intentando método de fallback");
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Error al deserializar JSON con modelo tipado, intentando método de fallback");
            }
            
            // Método de fallback: usar el método original ExtractPdfFromJson
            try
            {
                string? base64Content = ExtractPdfFromJson(jsonResponse);
                
                if (!string.IsNullOrEmpty(base64Content) && IsValidBase64(base64Content))
                {
                    result.PdfData = Convert.FromBase64String(base64Content);
                    result.Success = true;
                    _logger.LogInformation("PDF extraído exitosamente usando método de fallback");
                }
                else
                {
                    result.ErrorMessage = "No se pudo extraer contenido PDF válido usando ningún método";
                    _logger.LogError("Falló tanto el método tipado como el de fallback para extraer PDF");
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error en método de fallback: {ex.Message}";
                _logger.LogError(ex, "Error en método de fallback para extraer PDF");
            }
            
            return result;
        }

        /// <summary>
        /// Valida si una cadena es un base64 válido
        /// </summary>
        /// <param name="base64String">Cadena a validar</param>
        /// <returns>True si es base64 válido</returns>
        private bool IsValidBase64(string base64String)
        {
            if (string.IsNullOrWhiteSpace(base64String))
                return false;
                
            try
            {
                // Intentar convertir para validar
                Convert.FromBase64String(base64String);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Método alternativo para extraer el PDF (similar al script VBScript original)
        /// </summary>
        private string? ExtractPdfManual(string response)
        {
            try
            {
                string[] parts = response.Split(',');
                
                foreach (string part in parts)
                {
                    string cleanPart = part
                        .Replace("\\", "")
                        .Replace("{", "")
                        .Replace("}", "")
                        .Replace("[", "")
                        .Replace("]", "")
                        .Replace("\"", "");
                    
                    if (cleanPart.StartsWith("pdf:"))
                    {
                        return cleanPart.Substring(4); // Remover "pdf:"
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en extracción manual del PDF");
            }
            
            return null;
        }

        /// <summary>
        /// Extrae el contenido base64 del campo "pdf" del JSON usando System.Text.Json (método de fallback)
        /// </summary>
        private string? ExtractPdfFromJson(string jsonResponse)
        {
            try
            {
                // Opciones para el parser JSON
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // Intentar parsear como JsonDocument
                using (JsonDocument document = JsonDocument.Parse(jsonResponse))
                {
                    JsonElement root = document.RootElement;
                    
                    // Buscar el campo "pdf" en el JSON
                    if (root.TryGetProperty("pdf", out JsonElement pdfElement))
                    {
                        return pdfElement.GetString();
                    }

                    // Buscar de forma case-insensitive
                    foreach (JsonProperty property in root.EnumerateObject())
                    {
                        if (string.Equals(property.Name, "pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            return property.Value.GetString();
                        }
                    }

                    // Si el JSON es un array, buscar en cada elemento
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement element in root.EnumerateArray())
                        {
                            if (element.TryGetProperty("pdf", out JsonElement pdfInArray))
                            {
                                return pdfInArray.GetString();
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Error al parsear JSON, intentando método manual");
                // Si falla el parseo JSON, intentar el método manual
                return ExtractPdfManual(jsonResponse);
            }

            return null;
        }

        /// <summary>
        /// Construye la URL de la API WebPOS FEPA para descargar un PDF por número de factura
        /// </summary>
        /// <param name="noFactura">Número de factura</param>
        /// <returns>URL completa para la API</returns>
        public string BuildWebPosApiUrl(string noFactura)
        {
            // Construir la URL completa usando la configuración
            return $"{_webPOSSettings.BaseUrl}/{_webPOSSettings.Empresa}/{_webPOSSettings.Guid}/{noFactura}";
        }

        /// <summary>
        /// Método principal mejorado para descargar un PDF por número de factura
        /// Ahora utiliza el nuevo sistema con validaciones completas
        /// </summary>
        /// <param name="noFactura">Número de factura</param>
        /// <returns>Contenido del PDF en bytes o null si hay error</returns>
        public async Task<byte[]?> DownloadPdfAsync(string noFactura)
        {
            if (string.IsNullOrWhiteSpace(noFactura))
            {
                throw new ArgumentException("El número de factura no puede estar vacío", nameof(noFactura));
            }

            // Construir la URL usando la configuración
            string apiUrl = BuildWebPosApiUrl(noFactura);
            
            _logger.LogInformation("Descargando PDF para factura: {NoFactura} desde: {Url}", noFactura, apiUrl);
            
            // Usar el nuevo método mejorado
            var result = await DownloadPdfWithDetailsAsync(apiUrl);
            
            if (!result.Success)
            {
                _logger.LogError("Error al descargar PDF: {Error}", result.ErrorMessage);
                return null;
            }
            
            return result.PdfData;
        }

        /// <summary>
        /// Método público para obtener información detallada del PDF sin descargar el contenido
        /// </summary>
        /// <param name="url">URL completa de la API GetPdf de WebPOS</param>
        /// <returns>Información de la respuesta sin los datos binarios del PDF</returns>
        public async Task<WebPOSPdfResponse?> GetPdfInfoAsync(string url)
        {
            try
            {
                var result = await DownloadPdfWithDetailsAsync(url);
                return result.ResponseData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener información del PDF desde: {Url}", url);
                return null;
            }
        }

        /// <summary>
        /// Método para validar si un PDF está disponible sin descargarlo completamente
        /// </summary>
        /// <param name="noFactura">Número de factura</param>
        /// <returns>True si el PDF está disponible</returns>
        public async Task<bool> IsPdfAvailableAsync(string noFactura)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(noFactura))
                    return false;

                string apiUrl = BuildWebPosApiUrl(noFactura);
                var info = await GetPdfInfoAsync(apiUrl);
                
                return info?.PdfGenerated == true && !string.IsNullOrWhiteSpace(info.Pdf);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Método mejorado para descargar PDF con reintentos automáticos
        /// </summary>
        /// <param name="noFactura">Número de factura</param>
        /// <param name="maxRetries">Número máximo de reintentos</param>
        /// <returns>Contenido del PDF en bytes o null si hay error</returns>
        public async Task<byte[]?> DownloadPdfWithRetriesAsync(string noFactura, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Intento {Attempt} de {MaxRetries} para descargar PDF de factura: {NoFactura}", 
                        attempt, maxRetries, noFactura);
                    
                    var result = await DownloadPdfAsync(noFactura);
                    
                    if (result != null && result.Length > 0)
                    {
                        _logger.LogInformation("PDF descargado exitosamente en intento {Attempt}", attempt);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error en intento {Attempt} de {MaxRetries} para factura {NoFactura}", 
                        attempt, maxRetries, noFactura);
                }

                // Esperar antes del siguiente intento (excepto en el último)
                if (attempt < maxRetries)
                {
                    await Task.Delay(1000 * attempt); // Backoff progresivo
                }
            }

            _logger.LogError("Falló la descarga del PDF después de {MaxRetries} intentos para factura: {NoFactura}", 
                maxRetries, noFactura);
            return null;
        }

        /// <summary>
        /// Obtiene el nombre de archivo apropiado para el PDF descargado
        /// </summary>
        /// <param name="result">Resultado de la descarga con detalles</param>
        /// <param name="fallbackName">Nombre por defecto si no se encuentra uno en la respuesta</param>
        /// <returns>Nombre de archivo a usar</returns>
        public string GetPdfFileName(PdfExtractionResult result, string fallbackName)
        {
            // Prioridad 1: Nombre de archivo de la respuesta WebPOS
            if (!string.IsNullOrWhiteSpace(result.ExtractedFileName))
            {
                // Asegurar que tenga extensión .pdf
                if (!result.ExtractedFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return result.ExtractedFileName + ".pdf";
                }
                return result.ExtractedFileName;
            }

            // Prioridad 2: Generar nombre usando el fileName del ResponseData
            if (result.ResponseData?.FileName != null && !string.IsNullOrWhiteSpace(result.ResponseData.FileName))
            {
                if (!result.ResponseData.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return result.ResponseData.FileName + ".pdf";
                }
                return result.ResponseData.FileName;
            }

            // Prioridad 3: Generar nombre usando CUFE si está disponible
            if (result.ResponseData?.Cufe != null && !string.IsNullOrWhiteSpace(result.ResponseData.Cufe))
            {
                return $"Factura_{result.ResponseData.Cufe}_{DateTime.Now:yyyyMMdd}.pdf";
            }

            // Prioridad 4: Usar el nombre de fallback proporcionado
            return fallbackName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) 
                ? fallbackName 
                : fallbackName + ".pdf";
        }

        /// <summary>
        /// Método conveniente para obtener nombre de archivo desde número de factura
        /// </summary>
        /// <param name="noFactura">Número de factura</param>
        /// <returns>Nombre de archivo basado en la respuesta de WebPOS o un nombre por defecto</returns>
        public async Task<string> GetPdfFileNameAsync(string noFactura)
        {
            try
            {
                string apiUrl = BuildWebPosApiUrl(noFactura);
                var result = await DownloadPdfWithDetailsAsync(apiUrl);
                
                if (result.Success)
                {
                    return GetPdfFileName(result, $"Factura_{noFactura}_{DateTime.Now:yyyyMMdd}.pdf");
                }
                
                return $"Factura_{noFactura}_{DateTime.Now:yyyyMMdd}.pdf";
            }
            catch
            {
                return $"Factura_{noFactura}_{DateTime.Now:yyyyMMdd}.pdf";
            }
        }
    }

    /// <summary>
    /// Modelo completo para la respuesta JSON de la API WebPOS FEPA
    /// </summary>
    public class WebPOSPdfResponse
    {
        public bool PdfGenerated { get; set; }
        public string? CompanyLicCod { get; set; }
        public string? Cufe { get; set; }
        public string? Pdf { get; set; }
        public string? XmlFe { get; set; }
        public string? FileName { get; set; }
    }

    /// <summary>
    /// Resultado del procesamiento del PDF con información adicional
    /// </summary>
    public class PdfExtractionResult
    {
        public bool Success { get; set; }
        public byte[]? PdfData { get; set; }
        public string? ErrorMessage { get; set; }
        public WebPOSPdfResponse? ResponseData { get; set; }
        public string? ExtractedFileName { get; set; }
        public bool PdfWasGenerated { get; set; }
    }
}
