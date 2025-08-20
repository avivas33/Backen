using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Api_Celero.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Api_Celero.Services
{
    public interface IHansaReceiptService
    {
        Task<HansaReceiptResponse> GetReceiptDataAsync(string receiptNumber, string companyCode);
        Task<string> FindReceiptByInvoiceAndDateAsync(string invoiceNumber, string transactionDate);
    }

    public class HansaReceiptService : IHansaReceiptService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HansaReceiptService> _logger;

        public HansaReceiptService(HttpClient httpClient, IConfiguration configuration, ILogger<HansaReceiptService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<HansaReceiptResponse> GetReceiptDataAsync(string receiptNumber, string companyCode)
        {
            try
            {
                // Obtener configuración de Hansa
                var baseUrl = _configuration["Hansa:BaseUrl"];
                var webPort = _configuration["Hansa:WebPort"];
                var usuario = _configuration["Hansa:Usuario"];
                var clave = _configuration["Hansa:Clave"];
                var useBasicAuth = _configuration.GetValue<bool>("Hansa:UseBasicAuth");
                var timeoutSeconds = _configuration.GetValue<int>("Hansa:TimeoutSeconds", 30);

                // Construir la URL completa
                var url = $"{baseUrl}:{webPort}/api/{companyCode}/IPVc?sort=SerNr&range={receiptNumber}&fields=CurPayVal,TransDate,InvoiceNr,InvoiceOfficialSerNr,RecVal,CustName,PayMode,{receiptNumber}";

                _logger.LogInformation($"Consultando recibo {receiptNumber} en Hansa API: {url}");

                // Usar CancellationToken en lugar de modificar el timeout del httpClient compartido
                using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                // Crear el request
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Agregar autenticación básica si está configurada
                if (useBasicAuth)
                {
                    var authToken = Encoding.ASCII.GetBytes($"{usuario}:{clave}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));
                }

                // Agregar headers adicionales necesarios para Hansa
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("User-Agent", "Celero-API/1.0");

                // Si hay una empresa específica, agregarla como header (esto depende de cómo Hansa maneje las empresas)
                if (!string.IsNullOrEmpty(companyCode))
                {
                    request.Headers.Add("X-Company-Code", companyCode);
                }

                // Realizar la petición con el CancellationToken
                var response = await _httpClient.SendAsync(request, cancellationTokenSource.Token);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Respuesta exitosa de Hansa para recibo {receiptNumber}");
                    _logger.LogInformation($"=== CONTENIDO RESPUESTA HANSA === {content}");
                    
                    // Deserializar la respuesta
                    var receiptData = JsonConvert.DeserializeObject<HansaReceiptResponse>(content);
                    
                    if (receiptData?.Data?.IPVc == null || receiptData.Data.IPVc.Count == 0)
                    {
                        _logger.LogWarning($"No se encontraron datos para el recibo {receiptNumber}");
                        return null;
                    }

                    return receiptData;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Error al consultar Hansa API. Status: {response.StatusCode}, Error: {errorContent}");
                    throw new HttpRequestException($"Error al consultar el recibo: {response.StatusCode}");
                }
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, $"Timeout al consultar el recibo {receiptNumber} en Hansa");
                throw new TimeoutException($"La consulta del recibo {receiptNumber} excedió el tiempo límite", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al consultar el recibo {receiptNumber} en Hansa");
                throw;
            }
        }

        public async Task<string> FindReceiptByInvoiceAndDateAsync(string invoiceNumber, string transactionDate)
        {
            try
            {
                // Obtener configuración de Hansa
                var baseUrl = _configuration["Hansa:BaseUrl"];
                var webPort = _configuration["Hansa:WebPort"];
                var usuario = _configuration["Hansa:Usuario"];
                var clave = _configuration["Hansa:Clave"];
                var useBasicAuth = _configuration.GetValue<bool>("Hansa:UseBasicAuth");
                var timeoutSeconds = _configuration.GetValue<int>("Hansa:TimeoutSeconds", 30);

                // Construir la URL para buscar por fecha
                var url = $"{baseUrl}:{webPort}/api/2/IPVc?sort=TransDate&range={transactionDate}&fields=SerNr,InvoiceNr";

                _logger.LogInformation($"Buscando recibo por factura {invoiceNumber} en fecha {transactionDate}. URL: {url}");

                // Usar CancellationToken
                using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                // Crear el request
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Agregar autenticación básica si está configurada
                if (useBasicAuth)
                {
                    var authToken = Encoding.ASCII.GetBytes($"{usuario}:{clave}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));
                }

                // Agregar headers adicionales necesarios para Hansa
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("User-Agent", "Celero-API/1.0");

                // Realizar la petición
                var response = await _httpClient.SendAsync(request, cancellationTokenSource.Token);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Respuesta exitosa de búsqueda por fecha para factura {invoiceNumber}");
                    
                    // Deserializar la respuesta
                    var searchResult = JsonConvert.DeserializeObject<HansaReceiptResponse>(content);
                    
                    if (searchResult?.Data?.IPVc != null)
                    {
                        // Buscar el recibo que contenga la factura específica
                        foreach (var receipt in searchResult.Data.IPVc)
                        {
                            if (receipt.Rows != null)
                            {
                                foreach (var row in receipt.Rows)
                                {
                                    if (row.InvoiceNr == invoiceNumber)
                                    {
                                        _logger.LogInformation($"Recibo encontrado: SerNr={receipt.SerNr} para factura {invoiceNumber}");
                                        return receipt.SerNr;
                                    }
                                }
                            }
                        }
                    }

                    _logger.LogWarning($"No se encontró recibo para la factura {invoiceNumber} en la fecha {transactionDate}");
                    return null;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Error al buscar recibo por fecha. Status: {response.StatusCode}, Error: {errorContent}");
                    return null;
                }
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, $"Timeout al buscar recibo por factura {invoiceNumber} y fecha {transactionDate}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al buscar recibo por factura {invoiceNumber} y fecha {transactionDate}");
                return null;
            }
        }
    }
}