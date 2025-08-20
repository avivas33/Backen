using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api_Celero.Models;
using System.Net;
using System.Xml;
using Microsoft.Extensions.Configuration; // Para IConfiguration
using Api_Celero.Services; // Para 
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore; // Para ToListAsync
using Microsoft.Extensions.Options; // Para IOptions
using System.Globalization; // Para NumberStyles y CultureInfo

namespace Api_Celero.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientesController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ClientesController> _logger;
        private readonly IConfiguration _configuration; // Para leer appsettings
        private readonly IEmailService _emailService; // Para envío de emails
        private readonly IHansaReceiptService _hansaReceiptService; // Para consulta de recibos
        private readonly CobaltSettings _cobaltSettings;
        private readonly YappySettings _yappySettings;
        private readonly HansaSettings _hansaSettings;
        private readonly PayPalSettings _payPalSettings;

        private readonly PdfDownloaderService _pdfDownloaderService;
        private readonly ReciboCajaOfflineContext _dbContext;

        public ClientesController(
            IHttpClientFactory httpClientFactory,
            ILogger<ClientesController> logger,
            IConfiguration configuration,
            IEmailService emailService,
            IHansaReceiptService hansaReceiptService,
            IOptions<CobaltSettings> cobaltSettings,
            IOptions<YappySettings> yappySettings,
            IOptions<HansaSettings> hansaSettings,
            IOptions<PayPalSettings> payPalSettings,
            PdfDownloaderService pdfDownloaderService,
            ReciboCajaOfflineContext dbContext)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration; // Inyectar IConfiguration
            _emailService = emailService; // Inyectar EmailService
            _hansaReceiptService = hansaReceiptService; // Inyectar HansaReceiptService
            _cobaltSettings = cobaltSettings.Value;
            _yappySettings = yappySettings.Value;
            _hansaSettings = hansaSettings.Value;
            _payPalSettings = payPalSettings.Value;
            _pdfDownloaderService = pdfDownloaderService;
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string range = "CU-20037")
        {
            try
            {
                // Procesar el parámetro range para obtener el código correcto
                string processedRange = await ProcesarRangeParameter(range);

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(_hansaSettings.TimeoutSeconds);

                // Configurar autenticación básica usando la configuración
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_hansaSettings.Usuario}:{_hansaSettings.Clave}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

                // Configurar headers
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Construir URL usando la configuración
                string apiUrl = $"{_hansaSettings.GetFullBaseUrl()}/api/{_hansaSettings.CompanyCode}/CUVc?sort=Code&range={processedRange}&fields=Code,Name,Mobile,eMail,VatNr,CUType&filter.Closed=0";
                // CUType=1 filtro solo clientes
                var response = await httpClient.GetAsync(apiUrl);


                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Content(content, "application/json");
                }

                return StatusCode((int)response.StatusCode,
                    $"Error al obtener datos: {response.ReasonPhrase}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar la API externa");
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("facturas")]
        public async Task<IActionResult> GetFacturas(
            [FromQuery] string range,
            //[FromQuery] string fields = null,
            [FromQuery] string sort = null,
            [FromQuery(Name = "filter.CustCode")] string filterCustCode = null)
        {

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(_hansaSettings.TimeoutSeconds);

                // Configurar autenticación básica usando la configuración
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_hansaSettings.Usuario}:{_hansaSettings.Clave}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Construir URL usando la configuración con parámetros dinámicos
                var queryParams = new List<string>();
                
                // Siempre agregar sort primero si existe
                if (!string.IsNullOrEmpty(sort))
                    queryParams.Add($"sort={sort}");
                else
                    queryParams.Add("sort=InvDate"); // Default sort
                    
                // Range es requerido
                if (!string.IsNullOrEmpty(range))
                    queryParams.Add($"range={range}");
                
                // Fields
                //if (!string.IsNullOrEmpty(fields))
                //    queryParams.Add($"fields={fields}");
                //else
                    queryParams.Add("fields=SerNr,OfficialSerNr,PayDeal,InvDate,PayDate,Sum4,OKFlag,OrderNr,CustOrdNr,RefStr"); // Default fields
                
                // Filtro por código de cliente
                if (!string.IsNullOrEmpty(filterCustCode))
                    queryParams.Add($"filter.CustCode={filterCustCode}");
                
                var queryString = string.Join("&", queryParams);
                string apiUrl = $"{_hansaSettings.GetFullBaseUrl()}/api/{_hansaSettings.CompanyCode}/IVVc?{queryString}";
                var response = await httpClient.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Content(content, "application/json");
                }
                return StatusCode((int)response.StatusCode, $"Error al obtener facturas: {response.ReasonPhrase}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar facturas");
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("facturas/{companyCode}")]
        public async Task<IActionResult> GetFacturasPorEmpresa(
            string companyCode,
            [FromQuery] string range,
            [FromQuery] string fields = null,
            [FromQuery] string sort = null,
            [FromQuery(Name = "filter.CustCode")] string filterCustCode = null)
        {
            try
            {
                // Validar que la empresa existe y está activa
                var empresa = _hansaSettings.Companies.FirstOrDefault(c => c.CompCode == companyCode);
                if (empresa == null)
                {
                    return NotFound(new { message = $"Empresa con código '{companyCode}' no encontrada" });
                }

                if (empresa.ActiveStatus != "Activo")
                {
                    return BadRequest(new { message = $"La empresa '{companyCode}' no está activa" });
                }

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(_hansaSettings.TimeoutSeconds);

                // Configurar autenticación básica
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_hansaSettings.Usuario}:{_hansaSettings.Clave}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Construir URL con parámetros dinámicos
                var queryParams = new List<string>();
                
                // Siempre agregar sort primero si existe
                if (!string.IsNullOrEmpty(sort))
                    queryParams.Add($"sort={sort}");
                else
                    queryParams.Add("sort=InvDate"); // Default sort
                    
                // Range es requerido
                if (!string.IsNullOrEmpty(range))
                    queryParams.Add($"range={range}");

                // Fields
                if (!string.IsNullOrEmpty(fields))
                    //queryParams.Add($"fields={fields}");
                    queryParams.Add("fields=SerNr,OfficialSerNr,PayDeal,InvDate,PayDate,Sum4,OKFlag,OrderNr,CustOrdNr,RefStr");
                else
                    queryParams.Add("fields=SerNr,OfficialSerNr,PayDeal,InvDate,PayDate,Sum4,OKFlag,OrderNr,CustOrdNr,RefStr"); // Default fields
                
                // Filtro por código de cliente
                if (!string.IsNullOrEmpty(filterCustCode))
                    queryParams.Add($"filter.CustCode={filterCustCode}");
                
                // Filtrar solo facturas OK
                //queryParams.Add("filter.OKFlag=1");
                
                var queryString = string.Join("&", queryParams);
                string apiUrl = $"{_hansaSettings.GetFullBaseUrl()}/api/{companyCode}/IVVc?{queryString}";
                
                _logger.LogInformation("Consultando facturas para empresa {CompanyCode}: {Url}", companyCode, apiUrl);
                
                var response = await httpClient.GetAsync(apiUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var facturaResponse = System.Text.Json.JsonSerializer.Deserialize<Models.FacturaResponse>(content);
                    
                    // Procesar las facturas para agregar información de la empresa y forma de pago
                    if (facturaResponse?.data?.IVVc != null)
                    {
                        // Obtener formas de pago con PDType
                        var formasPagoResponse = await httpClient.GetAsync($"{_hansaSettings.GetFullBaseUrl()}/api/{companyCode}/PDVc?fields=Code,pdComment,PDType");
                        var pdTypeDict = new Dictionary<string, string>();
                        
                        if (formasPagoResponse.IsSuccessStatusCode)
                        {
                            var formasPagoContent = await formasPagoResponse.Content.ReadAsStringAsync();
                            var formasPago = System.Text.Json.JsonSerializer.Deserialize<Models.FormaPagoResponse>(formasPagoContent);
                            if (formasPago?.data?.PDVc != null)
                            {
                                pdTypeDict = formasPago.data.PDVc.ToDictionary(fp => fp.Code, fp => fp.PDType);
                            }
                        }
                        
                        // Enriquecer las facturas con información adicional
                        var facturasEnriquecidas = facturaResponse.data.IVVc.Select(f => {
                            // Buscar el PDType basado en el PayDeal de la factura
                            string pdType = null;
                            if (!string.IsNullOrEmpty(f.PayDeal) && pdTypeDict.ContainsKey(f.PayDeal))
                            {
                                pdType = pdTypeDict[f.PayDeal];
                            }
                            
                            return new
                            {
                                f.SerNr,
                                f.OfficialSerNr,
                                f.PayDeal,
                                PDType = pdType,
                                FormaPago = GetTipoDocumento(pdType),
                                f.InvDate,
                                f.PayDate,
                                f.Sum4,
                                CompanyCode = companyCode,
                                CompanyName = empresa.CompName,
                                CompanyShortName = empresa.ShortName ?? empresa.CompName
                            };
                        }).ToList();
                        
                        return Ok(new
                        {
                            success = true,
                            data = new
                            {
                                IVVc = facturasEnriquecidas,
                                totalCount = facturasEnriquecidas.Count
                            }
                        });
                    }
                    
                    return Content(content, "application/json");
                }
                
                return StatusCode((int)response.StatusCode, new
                {
                    message = $"Error al obtener facturas de la empresa {companyCode}",
                    details = response.ReasonPhrase
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar facturas para empresa {CompanyCode}", companyCode);
                return StatusCode(500, new
                {
                    message = "Error interno del servidor",
                    details = ex.Message
                });
            }
        }

        [HttpGet("facturas-abiertas")]
        public async Task<IActionResult> GetFacturasAbiertas([FromQuery] string range)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(_hansaSettings.TimeoutSeconds);

                // Configurar autenticación básica usando la configuración
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_hansaSettings.Usuario}:{_hansaSettings.Clave}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Construir URL usando la configuración
                string apiUrl = $"{_hansaSettings.GetFullBaseUrl()}/api/{_hansaSettings.CompanyCode}/ARVc?sort=CustCode&range={range}&fields=InvoiceNr,BookRVal";
                var response = await httpClient.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Content(content, "application/json");
                }
                return StatusCode((int)response.StatusCode, $"Error al obtener facturas abiertas: {response.ReasonPhrase}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar facturas abiertas");
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("facturas-filtradas")]
        public async Task<IActionResult> GetFacturasFiltradas([FromQuery] string range)
        {
            try
            {
                var resultadoConsolidado = new List<object>();
                var empresasConErrores = new List<object>();
                var empresasActivas = _hansaSettings.Companies.Where(c => c.ActiveStatus == "Activo").ToList();

                _logger.LogInformation("Procesando facturas filtradas para {Count} empresas activas", empresasActivas.Count);

                // Procesar empresas en paralelo para mejor rendimiento
                var tareas = empresasActivas.Select(async empresa =>
                {
                    try
                    {
                        _logger.LogInformation("Iniciando procesamiento para empresa: {CompName} ({CompCode})", empresa.CompName, empresa.CompCode);

                        var httpClient = _httpClientFactory.CreateClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(_hansaSettings.TimeoutSeconds);

                        // Configurar autenticación básica
                        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_hansaSettings.Usuario}:{_hansaSettings.Clave}"));
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        string baseApiUrl = $"{_hansaSettings.GetFullBaseUrl()}/api/{empresa.CompCode}";

                        // Crear las 3 consultas principales en paralelo
                        var consultaAbiertas = httpClient.GetAsync($"{baseApiUrl}/ARVc?sort=CustCode&range={range}&fields=InvoiceNr,BookRVal");
                        var consultaFacturas = httpClient.GetAsync($"{baseApiUrl}/IVVc?sort=CustCode&range={range}&fields=SerNr,OfficialSerNr,PayDeal,InvDate,PayDate,Sum4,Sum3,OrderNr,CustOrdNr,RefStr&filter.OKFlag=1");
                        var consultaFormasPago = httpClient.GetAsync($"{baseApiUrl}/PDVc?fields=Code,pdComment,Installment");

                        // Esperar todas las consultas principales en paralelo
                        var respuestas = await Task.WhenAll(consultaAbiertas, consultaFacturas, consultaFormasPago);
                        var respAbiertas = respuestas[0];
                        var respFacturas = respuestas[1];
                        var respFormasPago = respuestas[2];

                        // Verificar errores
                        if (!respAbiertas.IsSuccessStatusCode || !respFacturas.IsSuccessStatusCode || !respFormasPago.IsSuccessStatusCode)
                        {
                            var errores = new List<string>();
                            if (!respAbiertas.IsSuccessStatusCode) errores.Add($"Facturas abiertas: {respAbiertas.StatusCode}");
                            if (!respFacturas.IsSuccessStatusCode) errores.Add($"Facturas: {respFacturas.StatusCode}");
                            if (!respFormasPago.IsSuccessStatusCode) errores.Add($"Formas de pago: {respFormasPago.StatusCode}");

                            return new
                            {
                                Empresa = empresa.CompName,
                                CompCode = empresa.CompCode,
                                Error = $"Errores en consultas: {string.Join(", ", errores)}",
                                Facturas = new List<object>()
                            };
                        }

                        // Procesar respuestas en paralelo para mejor rendimiento
                        var procesamientoTareas = new Task<object>[]
                        {
                            Task.Run(async () =>
                            {
                                var json = await respAbiertas.Content.ReadAsStringAsync();
                                return (object)(System.Text.Json.JsonSerializer.Deserialize<Models.FacturaAbiertaResponse>(json) ?? new Models.FacturaAbiertaResponse());
                            }),
                            Task.Run(async () =>
                            {
                                var json = await respFacturas.Content.ReadAsStringAsync();
                                return (object)(System.Text.Json.JsonSerializer.Deserialize<Models.FacturaResponse>(json) ?? new Models.FacturaResponse());
                            }),
                            Task.Run(async () =>
                            {
                                var json = await respFormasPago.Content.ReadAsStringAsync();
                                return (object)(System.Text.Json.JsonSerializer.Deserialize<Models.FormaPagoResponse>(json) ?? new Models.FormaPagoResponse());
                            })
                        };

                        var resultados = await Task.WhenAll(procesamientoTareas);
                        var abiertas = resultados[0] as Models.FacturaAbiertaResponse;
                        var facturas = resultados[1] as Models.FacturaResponse;
                        var formasPago = resultados[2] as Models.FormaPagoResponse;

                        // Crear diccionarios optimizados para lookups rápidos
                        var abiertasSet = abiertas?.data?.ARVc?.Select(f => f.InvoiceNr).ToHashSet() ?? new HashSet<string>();
                        var abiertasDict = abiertas?.data?.ARVc?.ToDictionary(f => f.InvoiceNr, f => f.BookRVal) ?? new Dictionary<string, string>();
                        var formasPagoDict = formasPago?.data?.PDVc?.ToDictionary(fp => fp.Code, fp => fp) ?? new Dictionary<string, Models.FormaPago>();

                        // Log para debugging
                        _logger.LogInformation("Empresa {CompName}: Facturas abiertas encontradas: {Count}",
                            empresa.CompName, abiertasSet.Count);
                        _logger.LogInformation("Empresa {CompName}: Total facturas: {Count}",
                            empresa.CompName, facturas?.data?.IVVc?.Count ?? 0);

                        // Procesar facturas con lógica optimizada
                        var facturasEmpresa = new List<object>();
                        var filtradas = facturas?.data?.IVVc?.Where(f => abiertasSet.Contains(f.SerNr)).ToList() ?? new List<Models.Factura>();

                       

                        _logger.LogInformation("Empresa {CompName}: Facturas filtradas (que están abiertas): {Count}",
                            empresa.CompName, filtradas.Count);

                        foreach (var f in filtradas)
                        {
                            var formaPago = formasPagoDict.TryGetValue(f.PayDeal, out var fp) ? fp : null;
                            bool esCuotas = formaPago != null && !string.IsNullOrEmpty(formaPago.Installment) && formaPago.Installment != "0";

                            if (esCuotas)
                            {
                                // Procesar facturas con cuotas
                                var consultaCuotas = await httpClient.GetAsync($"{baseApiUrl}/ARInstallVc?sort=CustCode&range={range}&fields=DueDate,BookRVal");

                                if (consultaCuotas.IsSuccessStatusCode)
                                {
                                    var cuotasJson = await consultaCuotas.Content.ReadAsStringAsync();
                                    var cuotas = System.Text.Json.JsonSerializer.Deserialize<Models.CuotaResponse>(cuotasJson);
                                    var listaCuotas = cuotas?.data?.ARInstallVc ?? new List<Models.Cuota>();

                                    int numCuota = 1;
                                    foreach (var cuota in listaCuotas)
                                    {
                                        facturasEmpresa.Add(new
                                        {
                                            SerNr = f.SerNr,
                                            OfficialSerNr = f.OfficialSerNr,
                                            InvDate = f.InvDate,
                                            PayDate = cuota.DueDate,
                                            Sum4 = cuota.BookRVal,
                                            PayDeal = f.PayDeal,
                                            FormaPago = $"{numCuota} cuota{(listaCuotas.Count > 1 ? "s" : "")}",
                                            Installment = formaPago?.Installment,
                                            ITBMS = "0.00", // Para cuotas siempre es 0
                                            CompanyCode = empresa.CompCode,
                                            CompanyName = empresa.CompName,
                                            CompanyShortName = empresa.ShortName
                                        });
                                        numCuota++;
                                    }
                                }
                                else
                                {
                                    // Fallback sin cuotas
                                    string saldoReal = abiertasDict.TryGetValue(f.SerNr, out string? saldo) ? saldo : f.Sum4;
                                    facturasEmpresa.Add(CrearFacturaNormal(f, formaPago, saldoReal, abiertasDict, empresa));
                                }
                            }
                            else
                            {
                                // Facturas normales (sin cuotas)
                                string saldoReal = abiertasDict.TryGetValue(f.SerNr, out string? saldo) ? saldo : f.Sum4;
                                facturasEmpresa.Add(CrearFacturaNormal(f, formaPago, saldoReal, abiertasDict, empresa));
                            }
                        }

                        _logger.LogInformation("Empresa {CompName} procesada: {Count} facturas", empresa.CompName, facturasEmpresa.Count);

                        return new
                        {
                            Empresa = empresa.CompName,
                            CompCode = empresa.CompCode,
                            Error = string.Empty,
                            Facturas = facturasEmpresa
                        };
                    }
                    catch (Exception empresaEx)
                    {
                        _logger.LogError(empresaEx, "Error procesando empresa {CompName} ({CompCode})", empresa.CompName, empresa.CompCode);
                        return new
                        {
                            Empresa = empresa.CompName,
                            CompCode = empresa.CompCode,
                            Error = $"Error inesperado: {empresaEx.Message}",
                            Facturas = new List<object>()
                        };
                    }
                });

                // Esperar que todas las empresas terminen de procesarse
                var resultadosEmpresas = await Task.WhenAll(tareas);

                // Consolidar resultados y empresas con facturas
                var empresasConResultados = new List<string>();
                foreach (var resultado in resultadosEmpresas)
                {
                    if (!string.IsNullOrEmpty(resultado.Error))
                    {
                        empresasConErrores.Add(new
                        {
                            resultado.Empresa,
                            resultado.CompCode,
                            resultado.Error
                        });
                    }
                    else if (resultado.Facturas.Count > 0)
                    {
                        resultadoConsolidado.AddRange(resultado.Facturas);
                        empresasConResultados.Add(resultado.CompCode);
                    }
                }

                _logger.LogInformation("Procesamiento completado: {Total} facturas de {Empresas} empresas",
                    resultadoConsolidado.Count, empresasActivas.Count);

                // Preparar respuesta consolidada
                var result = new
                {
                    data = new
                    {
                        facturas = resultadoConsolidado.OrderBy(f => ((dynamic)f).CompanyCode).ThenBy(f => ((dynamic)f).InvDate),
                        totalFacturas = resultadoConsolidado.Count,
                        empresasConResultados = empresasConResultados,
                        empresasProcesadas = empresasActivas.Select(e => new
                        {
                            e.CompCode,
                            e.CompName,
                            e.ShortName,
                            e.ActiveStatus
                        }),
                        errores = empresasConErrores.Count > 0 ? empresasConErrores : null,
                        performance = new
                        {
                            procesamientoParalelo = true,
                            empresasEnParalelo = empresasActivas.Count,
                            empresasConFacturas = empresasConResultados.Count,
                            consultasSimultaneas = 3,
                            timestamp = DateTime.UtcNow
                        }
                    }
                };
                // Test temporal para ver la transformación de result a JSON y viceversa
                _logger.LogInformation("Test de transformación de result a JSON:");
                try
                {
                    var resultJsonTest = System.Text.Json.JsonSerializer.Serialize(result);
                    _logger.LogInformation("JSON serializado: {Json}", resultJsonTest);

                    var resultDeserialized = System.Text.Json.JsonSerializer.Deserialize<object>(resultJsonTest);
                    _logger.LogInformation("Objeto deserializado: {Obj}", resultDeserialized != null ? "OK" : "NULL");
                }
                catch (Exception exTest)
                {
                    _logger.LogError(exTest, "Error en test de transformación de result");
                }
                // Agregar test de result en json 

                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general en GetFacturasFiltradas");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor",
                    details = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        // Método auxiliar para crear facturas normales y optimizar código
        private object CrearFacturaNormal(Models.Factura f, Models.FormaPago? formaPago, string saldoReal,
            Dictionary<string, string> abiertasDict, Models.HansaCompany empresa)
        {
            // Calcular ITBMS: Si Sum4 == BookRVal entonces Sum3, sino 0
            string itbms = "0.00";
            if (abiertasDict.TryGetValue(f.OfficialSerNr, out string? bookRValStr) &&
                decimal.TryParse(f.Sum4, out var sum4Val) &&
                decimal.TryParse(bookRValStr, out var bookRVal) &&
                Math.Abs(sum4Val - bookRVal) < 0.01m) // Comparación con tolerancia para decimales
            {
                itbms = f.Sum3 ?? "0.00";
            }

            return new
            {
                SerNr = f.SerNr,
                OfficialSerNr = f.OfficialSerNr,
                InvDate = f.InvDate,
                PayDate = f.PayDate,
                Sum4 = saldoReal,
                PayDeal = f.PayDeal,
                FormaPago = formaPago?.pdComment ?? f.PayDeal,
                Installment = formaPago?.Installment,
                ITBMS = itbms,
                CompanyCode = empresa.CompCode,
                CompanyName = empresa.CompName,
                CompanyShortName = empresa.ShortName
            };
        }

        [HttpPost("recibos")]
        public async Task<IActionResult> CrearRecibo([FromBody] ReciboRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Crear el diccionario usando la estructura de tu función existente
                var entry = new Entry
                {
                    SerNr = request.SerNr,
                    TransDate = request.TransDate, // string a string
                    PayMode = request.PayMode,
                    Person = request.Person,
                    CUCode = request.CUCode,
                    RefStr = request.RefStr,
                    Detalles = request.Detalles.Select(d => new Detalle
                    {
                        InvoiceNr = d.InvoiceNr,
                        Sum = d.Sum.ToString(),
                        Objects = d.Objects,
                        Stp = d.Stp
                    }).ToList()
                };

                var datos = new Dictionary<string, Entry> { { request.SerNr, entry } };                // Llamar a la función de registro de recibos
                string? serNr = await AgregarRecibosDeCajasAsync(datos, _hansaSettings, _logger, _hansaReceiptService);

                if (!string.IsNullOrEmpty(serNr))
                {
                    // Determinar el método de pago basado en PayMode
                    string metodoPago = request.PayMode switch
                    {
                        "PP" => "PAYPAL",
                        "YP" => "YAPPY",
                        "TC" => "TARJETA DE CRÉDITO",
                        "ACH" => "ACH",
                        _ => request.PayMode
                    };
                    
                    // Intentar obtener el email del cliente si no viene en el request
                    string emailCliente = request.Email ?? "";
                    if (string.IsNullOrEmpty(emailCliente) && !string.IsNullOrEmpty(request.CUCode))
                    {
                        try
                        {
                            using (var scope = HttpContext.RequestServices.CreateScope())
                            {
                                var db = scope.ServiceProvider.GetRequiredService<ReciboCajaOfflineContext>();
                                var clienteLocal = await db.ClientesLocales
                                    .FirstOrDefaultAsync(c => c.Code == request.CUCode);
                                
                                if (clienteLocal != null && !string.IsNullOrEmpty(clienteLocal.eMail))
                                {
                                    emailCliente = clienteLocal.eMail;
                                    _logger.LogInformation("Email del cliente obtenido de base de datos local: {Email} para código: {Code}", 
                                        emailCliente, request.CUCode);
                                }
                            }
                        }
                        catch (Exception exEmail)
                        {
                            _logger.LogWarning(exEmail, "Error al obtener email del cliente desde base de datos");
                        }
                    }
                    
                    // Calcular el monto total del recibo
                    decimal montoTotal = 0;
                    if (request.Detalles != null && request.Detalles.Any())
                    {
                        montoTotal = request.Detalles.Sum(d => d.Sum);
                    }
                    
                    // Obtener el primer número de factura si existe
                    string numeroFactura = request.Detalles?.FirstOrDefault()?.InvoiceNr ?? "";
                    
                    // Enviar notificación usando el método reutilizable
                    await EnviarNotificacionReciboAsync(
                        serNr, 
                        emailCliente, 
                        metodoPago,
                        montoTotal,
                        numeroFactura,
                        request.CUCode ?? request.Person
                    );
                    
                    return Ok(new ReciboResponse
                    {
                        Success = true,
                        Message = "Recibo creado exitosamente",
                        SerNr = serNr
                    });
                }
                else
                {
                    // Guardar en SQLite si falla el registro
                    try
                    {
                        using (var scope = HttpContext.RequestServices.CreateScope())
                        {
                            var db = scope.ServiceProvider.GetRequiredService<ReciboCajaOfflineContext>();
                            var reciboOffline = new Models.ReciboCajaOffline
                            {
                                SerNr = request.SerNr,
                                TransDate = DateTime.TryParse(request.TransDate, out var fechaOffline) ? fechaOffline : DateTime.UtcNow,
                                PayMode = request.PayMode,
                                Person = request.Person,
                                CUCode = request.CUCode,
                                RefStr = request.RefStr,
                                DetallesJson = System.Text.Json.JsonSerializer.Serialize(request.Detalles)
                            };
                            db.RecibosOffline.Add(reciboOffline);
                            await db.SaveChangesAsync();
                        }
                        return StatusCode(202, new ReciboResponse
                        {
                            Success = false,
                            Message = "No se pudo registrar el recibo en el sistema principal, pero se guardó offline.",
                            SerNr = request.SerNr
                        });
                    }
                    catch (Exception exSqlite)
                    {
                        _logger.LogError(exSqlite, "Error al guardar recibo offline en SQLite");
                        return StatusCode(500, new ReciboResponse
                        {
                            Success = false,
                            Message = $"Error al crear el recibo y al guardar offline: {exSqlite.Message}"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear recibo");
                return StatusCode(500, new ReciboResponse
                {
                    Success = false,
                    Message = $"Error interno: {ex.Message}"
                });
            }
        }
        private static async Task<string?> AgregarRecibosDeCajasAsync(Dictionary<string, Entry> datos, HansaSettings hansaSettings, ILogger<ClientesController> logger = null, IHansaReceiptService hansaReceiptService = null)
        {
            string? Retorno = null; // Change 'string Retorno = null;' to 'string? Retorno = null;' to allow nullable types.
            var httpClientHandler = new HttpClientHandler()
            {
                Credentials = new NetworkCredential(hansaSettings.Usuario, hansaSettings.Clave),
            };

            var httpClient = new HttpClient(httpClientHandler);
            var builder = new UriBuilder($"{hansaSettings.GetFullBaseUrl()}/api/{hansaSettings.CompanyCode}/IPVc?");
            var _url = builder.ToString().Trim();
            httpClient.BaseAddress = new Uri(_url);

            List<string> itemsList = new List<string>();
            string Maestro = string.Empty;
            int i = 0;
            string Comment = string.Empty;
            foreach (var entry in datos.Values)
            {
                // Usar el campo Objects del primer detalle como comentario si está disponible
                string OKFlag = "1";
                var primerDetalle = entry.Detalles?.FirstOrDefault();
                if (!string.IsNullOrEmpty(primerDetalle?.Objects))
                {
                    Comment = primerDetalle.Objects;
                }
                else
                {
                    // Fallback al comentario por defecto basado en PayMode
                    if (entry.PayMode == "YP")
                    {
                        Comment = "Pago Yappy: " + entry.SerNr;
                    }
                    else if (entry.PayMode == "PP")
                    {
                        Comment = "Pago PayPal: " + entry.SerNr;
                    }
                     else if (entry.PayMode == "ACH")
                    {
                        OKFlag = "0";
                    }
                    else
                    {
                        Comment = "TARJETA DE CREDITO/DEBITO POR COBALT";
                    }
                }

                // Usar fecha actual pero ajustada para evitar problemas de timezone con Hansa
                DateTime TransDate = DateTime.Now; // Usar fecha de ayer para evitar problemas de futuro
               

                // Construcción del encabezado con valores reales
                Maestro =
                  $"&set_field.TransDate={Uri.EscapeDataString(TransDate.ToString("yyyy-MM-dd"))}" +
                  $"&set_field.RegDate={Uri.EscapeDataString(TransDate.ToString("yyyy-MM-dd"))}" +
                  $"&set_field.PayMode={Uri.EscapeDataString(entry.PayMode)}" +
                  $"&set_field.RecNumber={Uri.EscapeDataString(entry.RefStr)}" +
                 // $"&set_field.Comment={Uri.EscapeDataString(Comment)}" +
                  $"&set_field.OKFlag={Uri.EscapeDataString(OKFlag)}";

                // Construir Detalles
                foreach (var detalle in entry.Detalles)
                {
                    // Usar el monto tal como viene, sin re-formateo
                    string montoFormateado = detalle.Sum;
                    
                    
                    string Item = $"&set_row_field.{i}.InvoiceNr={detalle.InvoiceNr}" +
                      $"&set_row_field.{i}.RecVal={montoFormateado}" +
                      $"&set_row_field.{i}.PayDate={TransDate.ToString("yyyy-MM-dd")}" +
                      $"&set_row_field.{i}.Stp={detalle.Stp}";

                    itemsList.Add(Item);
                    i++;
                }
            }

            string Mensaje = Maestro + string.Join("", itemsList);

            var request = new HttpRequestMessage(HttpMethod.Post, _url)
            {
                Content = new StringContent(Mensaje, Encoding.UTF8, "application/x-www-form-urlencoded")
            };

            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    string dataXml = await response.Content.ReadAsStringAsync();

                    // Intentar extraer SerNr con manejo robusto de XML
                    string? serNrEncontrado = null;
                    try 
                    {
                        // Buscar SerNr directamente en el string XML sin parsear
                        var serNrMatch = System.Text.RegularExpressions.Regex.Match(dataXml, @"<SerNr[^>]*>([^<]+)</SerNr>");
                        if (serNrMatch.Success)
                        {
                            serNrEncontrado = serNrMatch.Groups[1].Value;
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                    
                    // Si encontramos SerNr, usarlo (incluso con warnings como 20878)
                    if (!string.IsNullOrEmpty(serNrEncontrado))
                    {
                        Retorno = serNrEncontrado;
                    }
                    // Si no hay SerNr pero hay error 20878, significa que falló
                    else if (dataXml.Contains("<error"))
                    {
                        // Nota: Este XML no tiene un elemento raíz, así que lo envolvemos
                        string wrappedXml = $"<root>{dataXml.Replace("<?xml version='1.0' encoding='UTF-8' standalone='yes'?>", "")}</root>";

                        XDocument doc = XDocument.Parse(wrappedXml);

                        string? codigoError = doc.Descendants("error").FirstOrDefault()?.Attribute("code")?.Value;

                        if (codigoError == "20878")
                        {
                            // Error 20878 "Monto Demasiado Alto" - el recibo puede haberse registrado exitosamente
                            // Intentar buscar el recibo real usando búsqueda por fecha y factura
                            logger?.LogWarning("Error 20878 detectado, intentando búsqueda alternativa del recibo...");
                            
                            try
                            {
                                // Obtener el número de factura del primer detalle
                                var primerDetalle = datos.Values.FirstOrDefault()?.Detalles?.FirstOrDefault();
                                if (primerDetalle != null && !string.IsNullOrEmpty(primerDetalle.InvoiceNr))
                                {
                                    // Usar la fecha de la transacción para buscar
                                    string fechaBusqueda = DateTime.Now.ToString("yyyy-MM-dd");
                                    
                                    var serNrReal = hansaReceiptService != null ? await hansaReceiptService.FindReceiptByInvoiceAndDateAsync(
                                        primerDetalle.InvoiceNr, 
                                        fechaBusqueda
                                    ) : null;
                                    
                                    if (!string.IsNullOrEmpty(serNrReal))
                                    {
                                        logger?.LogInformation($"¡Recibo encontrado via búsqueda alternativa! SerNr: {serNrReal} para factura {primerDetalle.InvoiceNr}");
                                        Retorno = serNrReal;
                                    }
                                    else
                                    {
                                        logger?.LogWarning($"No se encontró recibo para factura {primerDetalle.InvoiceNr} en fecha {fechaBusqueda}");
                                        Retorno = codigoError; // Fallback al código de error
                                    }
                                }
                                else
                                {
                                    logger?.LogWarning("No se puede realizar búsqueda alternativa: no hay número de factura");
                                    Retorno = codigoError; // Fallback al código de error
                                }
                            }
                            catch (Exception exBusqueda)
                            {
                                logger?.LogError(exBusqueda, "Error durante búsqueda alternativa del recibo");
                                Retorno = codigoError; // Fallback al código de error
                            }
                        }
                    }
                    else
                    {
                        using (var stringReader = new StringReader(dataXml))
                        {
                            using (var xmlReader = System.Xml.XmlReader.Create(stringReader))
                            {
                                while (xmlReader.Read())
                                {
                                    if (xmlReader.IsStartElement() && xmlReader.Name == "SerNr")
                                    {
                                        Retorno = xmlReader.ReadElementContentAsString();
                                    }
                                }
                            }
                        }
                    }

                    // Extraer el valor de SerNr del XML

                }
                catch (Exception ex)
                {
                }
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync();
            }

            return Retorno;
        }

        /// <summary>
        /// Método reutilizable para enviar notificación de recibo por correo
        /// Se llama después de registrar exitosamente cualquier recibo (PayPal, Tarjeta, Yappy)
        /// </summary>
        /// <param name="serNrRegistrado">Número del recibo registrado en Hansa</param>
        /// <param name="emailCliente">Email del cliente (puede venir de diferentes fuentes)</param>
        /// <param name="metodosPago">Método de pago usado (PAYPAL, TARJETA, YAPPY, etc)</param>
        /// <param name="montoFallback">Monto a usar si no se puede obtener de Hansa</param>
        /// <param name="numeroFactura">Número de factura para usar como fallback</param>
        /// <param name="clienteCode">Código del cliente para usar como fallback del nombre</param>
        private async Task EnviarNotificacionReciboAsync(
            string serNrRegistrado, 
            string emailCliente, 
            string metodosPago,
            decimal montoFallback,
            string numeroFactura = "",
            string clienteCode = "",
            string nombreClienteReal = "")
        {
            if (string.IsNullOrEmpty(serNrRegistrado))
            {
                _logger.LogWarning("No se puede enviar notificación: serNrRegistrado está vacío");
                return;
            }

            // Excluir ACH del envío automático de correos
            if (metodosPago?.ToUpper() == "ACH")
            {
                _logger.LogInformation("No se envía notificación automática para pagos ACH según configuración");
                return;
            }

            if (string.IsNullOrEmpty(emailCliente))
            {
                _logger.LogInformation("No se envió notificación de recibo porque no se tiene el email del cliente");
                return;
            }

            try
            {
                _logger.LogInformation("Enviando notificación de recibo {SerNr} a {Email} - Método: {MetodoPago}", 
                    serNrRegistrado, emailCliente, metodosPago);
                
                // Inicializar el request con valores vacíos que se llenarán desde Hansa
                var receiptEmailRequest = new ReceiptEmailRequest
                {
                    ReceiptNumber = serNrRegistrado,
                    CompanyCode = _hansaSettings.CompanyCode,
                    CustomerEmail = emailCliente,
                    CustomerName = "", // Se llenará desde Hansa
                    TransactionDate = "",  // Se llenará desde Hansa
                    PaymentMethod = "",  // Se llenará desde Hansa
                    TotalAmount = 0,  // Se llenará desde Hansa
                    Details = new List<ReceiptDetailItem>()
                };
                
                bool datosObtenidos = false;
                
                // Consultar Hansa para obtener TODOS los detalles del recibo
                try
                {
                    _logger.LogInformation("Consultando detalles del recibo {SerNr} en Hansa", serNrRegistrado);
                    var hansaReceipt = await _hansaReceiptService.GetReceiptDataAsync(serNrRegistrado, _hansaSettings.CompanyCode);
                    
                    _logger.LogInformation("Respuesta de Hansa para recibo {SerNr}: {@HansaResponse}", serNrRegistrado, hansaReceipt);
                    
                    if (hansaReceipt?.Data?.IPVc != null && hansaReceipt.Data.IPVc.Any())
                    {
                        var ipcvData = hansaReceipt.Data.IPVc.First();
                        
                        // Obtener el nombre del cliente desde Hansa (SIEMPRE desde Hansa)
                        if (ipcvData.Rows != null && ipcvData.Rows.Any())
                        {
                            receiptEmailRequest.CustomerName = ipcvData.Rows.First().CustName ?? clienteCode;
                            _logger.LogInformation("Nombre del cliente obtenido de Hansa: {CustomerName}", receiptEmailRequest.CustomerName);
                        }
                        
                        // Obtener la fecha de transacción
                        if (!string.IsNullOrEmpty(ipcvData.TransDate))
                        {
                            if (DateTime.TryParse(ipcvData.TransDate, out var transDate))
                            {
                                receiptEmailRequest.TransactionDate = transDate.ToString("dd/MM/yyyy");
                            }
                        }
                        
                        // Si no se pudo obtener la fecha, usar la actual
                        if (string.IsNullOrEmpty(receiptEmailRequest.TransactionDate))
                        {
                            receiptEmailRequest.TransactionDate = DateTime.Now.ToString("dd/MM/yyyy");
                        }
                        
                        // Mapear el método de pago
                        if (!string.IsNullOrEmpty(ipcvData.PayMode))
                        {
                            receiptEmailRequest.PaymentMethod = ipcvData.PayMode.ToUpper() switch
                            {
                                "PP" => "PAYPAL",
                                "YP" => "YAPPY",
                                "TC" => "TARJETA DE CRÉDITO/COBALT",
                                "TD" => "TARJETA DE DÉBITO",
                                "CR" => "TARJETA DE CRÉDITO",
                                _ => ipcvData.PayMode
                            };
                        }
                        else
                        {
                            // Usar el método de pago pasado como parámetro
                            receiptEmailRequest.PaymentMethod = metodosPago;
                        }
                        
                        // Obtener el monto total
                        if (decimal.TryParse(ipcvData.CurPayVal, 
                            System.Globalization.NumberStyles.Any, 
                            System.Globalization.CultureInfo.InvariantCulture, 
                            out var totalAmount))
                        {
                            // Convertir centavos a dólares si es necesario
                            receiptEmailRequest.TotalAmount = totalAmount;
                        }
                        else
                        {
                            receiptEmailRequest.TotalAmount = montoFallback;
                        }
                        
                        // Obtener los detalles del recibo
                        if (ipcvData.Rows != null && ipcvData.Rows.Any())
                        {
                            receiptEmailRequest.Details = ipcvData.Rows.Select(row => new ReceiptDetailItem
                            {
                                Reference = row.InvoiceNr,
                                CufeNumber = row.InvoiceOfficialSerNr ?? "",
                                Quota = "0", // Siempre 0 según especificación del usuario
                                ReceivedAmount = decimal.TryParse(row.RecVal, 
                                    System.Globalization.NumberStyles.Any, 
                                    System.Globalization.CultureInfo.InvariantCulture, 
                                    out var recVal) ? recVal  : 0  // Convertir centavos a dólares
                            }).ToList();
                            
                            datosObtenidos = true;
                            _logger.LogInformation("Detalles del recibo obtenidos exitosamente de Hansa. {DetallesCount} items. Data: {@DetallesData}", 
                                receiptEmailRequest.Details.Count, receiptEmailRequest.Details);
                        }
                    }
                }
                catch (Exception exHansa)
                {
                    _logger.LogWarning(exHansa, "No se pudieron obtener detalles del recibo desde Hansa");
                }
                
                // Si no se pudieron obtener datos de Hansa, usar datos básicos
                if (!datosObtenidos)
                {
                    _logger.LogWarning("Usando datos básicos para la notificación ya que no se pudieron obtener de Hansa");
                    
                    // Convertir centavos a dólares para PayPal, Yappy y Cobalt
                    decimal montoEnDolares = montoFallback;
                    if (metodosPago?.ToUpper() == "PAYPAL" || metodosPago?.ToUpper() == "PP" || 
                        metodosPago?.ToUpper() == "YAPPY" || metodosPago?.ToUpper() == "YP" ||
                        metodosPago?.ToUpper() == "CREDITCARD" || metodosPago?.ToUpper() == "CC")
                    {
                        montoEnDolares = montoFallback ; // Convertir centavos a dólares
                        _logger.LogInformation("Convirtiendo monto de centavos {MontoOriginal} a dólares {MontoDolares} para método {MetodoPago}", 
                            montoFallback, montoEnDolares, metodosPago);
                    }

                    if (metodosPago == "CREDITCARD")
                    {
                        metodosPago = "TARJETA DE CREDITO/DEBITO POR COBALT";
                    }
                    // Usar el nombre real del cliente si está disponible, sino usar el clienteCode
                    var nombreCliente = !string.IsNullOrEmpty(nombreClienteReal) ? nombreClienteReal : clienteCode;
                    _logger.LogInformation("Usando nombre para notificación: {NombreCliente} (Método: {MetodoPago})", nombreCliente, metodosPago);
                    
                    receiptEmailRequest.CustomerName = nombreCliente;
                    receiptEmailRequest.TransactionDate = DateTime.Now.ToString("dd-MM-yyyy");
                    receiptEmailRequest.PaymentMethod = metodosPago;
                    receiptEmailRequest.TotalAmount = montoEnDolares;
                    
                    if (!string.IsNullOrEmpty(numeroFactura))
                    {
                        receiptEmailRequest.Details = new List<ReceiptDetailItem>
                        {
                            new ReceiptDetailItem
                            {
                                Reference = numeroFactura,
                                CufeNumber = "",
                                Quota = "0",
                                ReceivedAmount = montoEnDolares
                            }
                        };
                    }
                }
                
                // Enviar el email
                var emailResult = await _emailService.SendReceiptEmailAsync(receiptEmailRequest);
                
                if (emailResult.Success)
                {
                    _logger.LogInformation("Notificación de recibo enviada exitosamente a {Email}. EmailId: {EmailId}", 
                        emailCliente, emailResult.EmailId);
                }
                else
                {
                    _logger.LogWarning("No se pudo enviar la notificación de recibo a {Email}. Error: {Error}", 
                        emailCliente, emailResult.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de recibo {SerNr} a {Email}", serNrRegistrado, emailCliente);
                // No fallar la transacción por error en el envío de email
            }
        }

        [HttpPost("venta-tarjeta")]
        public async Task<IActionResult> VentaTarjeta([FromBody] CobaltSaleRequest venta)
        {
            try
            {
                // 0. Validar InvoiceDetails (estándar obligatorio)
                if (venta.InvoiceDetails == null || venta.InvoiceDetails.Count == 0)
                {
                    _logger.LogError("ATENCIÓN: InvoiceDetails es requerido para tarjetas de crédito");
                    return BadRequest(new { success = false, message = "InvoiceDetails es requerido con el número de factura y monto específico para cada factura." });
                }

                // Validar que el total coincida con la suma de facturas
                decimal totalFacturas = venta.InvoiceDetails.Sum(d => d.Amount);
                if (decimal.TryParse(venta.amount, out decimal montoSolicitado))
                {
                    // Convertir centavos a dólares para comparar
                    decimal montoEnDolares = montoSolicitado / 100;
                    if (Math.Abs(totalFacturas - montoEnDolares) > 0.01m) // Tolerancia de 1 centavo
                    {
                        _logger.LogError("Monto total ({MontoSolicitado}) no coincide con suma de facturas ({TotalFacturas})", 
                            montoEnDolares, totalFacturas);
                        return BadRequest($"El monto total ({montoEnDolares:F2}) no coincide con la suma de facturas ({totalFacturas:F2})");
                    }
                }

                _logger.LogInformation($"Procesando pago Cobalt con {venta.InvoiceDetails.Count} facturas. Total: {totalFacturas:F2}");

                // 1. Validar y obtener código de empresa
                if (string.IsNullOrEmpty(venta.company_code))
                {
                    return BadRequest("El código de empresa es requerido para procesar el pago");
                }

                // 2. Obtener credenciales específicas para la empresa
                var credentials = _cobaltSettings.GetCredentials(venta.company_code);
                if (credentials == null)
                {
                    _logger.LogError("No se encontraron credenciales Cobalt para la empresa: {CompanyCode}", venta.company_code);
                    return BadRequest($"No hay credenciales Cobalt configuradas para la empresa {venta.company_code}");
                }

                _logger.LogInformation("Procesando pago Cobalt para empresa: {CompanyCode}", venta.company_code);

                // 3. Obtener token con credenciales específicas de la empresa
                var tokenRequest = new CobaltTokenRequest
                {
                    grant_type = "client_credentials",
                    client_id = credentials.ClientId,
                    client_secret = credentials.ClientSecret
                };

                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("selfservice/1.0.1");
                http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                http.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

                // Implementar resiliencia con reintentos
                const int maxRetries = 3;
                HttpResponseMessage tokenResp = null;
                
                for (int retry = 0; retry < maxRetries; retry++)
                {
                    try
                    {
                        tokenResp = await http.PostAsync(
                            _cobaltSettings.TokenUrl,
                            new StringContent(System.Text.Json.JsonSerializer.Serialize(tokenRequest), Encoding.UTF8, "application/json")
                        );

                        if (tokenResp.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Token obtenido exitosamente en intento {Attempt}", retry + 1);
                            break;
                        }

                        _logger.LogWarning("Error al obtener token de Cobalt en intento {Attempt}: {StatusCode}", retry + 1, tokenResp.StatusCode);
                        
                        if (retry < maxRetries - 1)
                        {
                            var waitTime = (retry + 1) * 1000; // Espera incremental: 1s, 2s, 3s
                            _logger.LogInformation("Esperando {WaitTime}ms antes de reintentar...", waitTime);
                            await Task.Delay(waitTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Excepción al obtener token de Cobalt en intento {Attempt}", retry + 1);
                        
                        if (retry < maxRetries - 1)
                        {
                            var waitTime = (retry + 1) * 1000;
                            await Task.Delay(waitTime);
                        }
                        else
                        {
                            return StatusCode(500, $"Error al conectar con Cobalt después de {maxRetries} intentos");
                        }
                    }
                }

                if (tokenResp == null || !tokenResp.IsSuccessStatusCode)
                {
                    _logger.LogError("No se pudo obtener token de Cobalt después de {MaxRetries} intentos", maxRetries);
                    return StatusCode((int)(tokenResp?.StatusCode ?? HttpStatusCode.ServiceUnavailable), $"No se pudo obtener el token de Cobalt después de {maxRetries} intentos");
                }

                var tokenJson = await tokenResp.Content.ReadAsStringAsync();
                var token = System.Text.Json.JsonSerializer.Deserialize<CobaltTokenResponse>(tokenJson);

                if (token == null || string.IsNullOrEmpty(token.access_token))
                {
                    _logger.LogError("Token inválido recibido de Cobalt");
                    return StatusCode(500, "Token inválido de Cobalt");
                }                // 2. Preparar request en formato exacto de Cobalt API
                var cobaltRequest = venta.ToCobaltApiFormat();

                // Log del payload que se enviará a Cobalt (sin datos sensibles)
                _logger.LogInformation("Enviando a Cobalt para empresa {CompanyCode}: currency={currency}, amount={amount}, tax={tax}, tip={tip}, holder={holder}",
                    venta.company_code, cobaltRequest.currency_code, cobaltRequest.amount, cobaltRequest.tax, cobaltRequest.tip, cobaltRequest.card_holder);

                // 3. Enviar venta a Cobalt con el formato exacto requerido
                var ventaHttp = new HttpClient();
                ventaHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);
                ventaHttp.DefaultRequestHeaders.UserAgent.ParseAdd("CobaltSale/1.0.1");
                ventaHttp.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var ventaResp = await ventaHttp.PostAsync(
                    _cobaltSettings.SaleUrl,
                    new StringContent(System.Text.Json.JsonSerializer.Serialize(cobaltRequest), Encoding.UTF8, "application/json")
                );

                var ventaResult = await ventaResp.Content.ReadAsStringAsync();
                if (!ventaResp.IsSuccessStatusCode)
                {
                    _logger.LogError("Error en venta Cobalt: {StatusCode} - {Response}", ventaResp.StatusCode, ventaResult);
                    return StatusCode((int)ventaResp.StatusCode, ventaResult);
                }
                _logger.LogInformation("Venta Cobalt exitosa");

                // 3. Enviar notificaciones por correo de forma asíncrona (fire-and-forget)
                // Esto NO bloquea la respuesta del endpoint
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Deserializar respuesta para obtener detalles del pago
                        var ventaRespuesta = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(ventaResult);

                        // Verificar condiciones para envío de correos:
                        // 1. "status": "ok" - Estado general exitoso
                        // 2. "data": { "status": "authorized" } - Transacción autorizada
                        bool debeEnviarCorreos = false;
                        string estadoGeneral = "";
                        string estadoTransaccion = "";

                        if (ventaRespuesta.TryGetProperty("status", out var statusElement))
                        {
                            estadoGeneral = statusElement.GetString() ?? "";
                        }

                        if (ventaRespuesta.TryGetProperty("data", out var dataElement))
                        {
                            if (dataElement.TryGetProperty("status", out var dataStatusElement))
                            {
                                estadoTransaccion = dataStatusElement.GetString() ?? "";
                            }
                        }

                        // Solo enviar correos si ambas condiciones se cumplen
                        debeEnviarCorreos = estadoGeneral.Equals("ok", StringComparison.OrdinalIgnoreCase) &&
                                           estadoTransaccion.Equals("authorized", StringComparison.OrdinalIgnoreCase);

                        _logger.LogInformation("Estados de transacción - General: {estadoGeneral}, Transacción: {estadoTransaccion}, Enviar correos: {debeEnviar}",
                            estadoGeneral, estadoTransaccion, debeEnviarCorreos);

                        if (debeEnviarCorreos)
                        {
                            // Extraer información del pago para los correos
                            var fechaPago = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                            // El monto viene en centavos, dividir entre 100 para obtener dólares
                            var monto = decimal.TryParse(venta.amount, out var amt) ? (amt / 100).ToString("F2") : venta.amount ?? "N/A";
                            var moneda = venta.currency_code ?? "USD";
                            var transactionId = "";
                            var authNumber = "";

                            // Intentar extraer información adicional de la respuesta
                            if (dataElement.ValueKind != JsonValueKind.Undefined)
                            {
                                if (dataElement.TryGetProperty("id", out var idElement))
                                    transactionId = idElement.ToString();
                                if (dataElement.TryGetProperty("authorization_number", out var authElement))
                                    authNumber = authElement.GetString() ?? "";
                            }

                            // *** NUEVO: Registrar recibo en Hansa usando el estándar de múltiples facturas ***
                            try
                            {
                                // Crear el recibo usando el estándar de múltiples facturas
                                var entry = new Entry
                                {
                                    SerNr = transactionId,
                                    TransDate = DateTime.Now.ToString("dd-MM-yyyy"),
                                    PayMode = "CC", // CC para Credit Card
                                    Person = venta.contract_number ?? "CARD_CLIENT",
                                    CUCode = venta.contract_number ?? "CARD_CLIENT",
                                    RefStr = transactionId,
                                    Detalles = new List<Detalle>()
                                };
                                
                                // Crear un detalle para cada factura
                                foreach (var invoiceDetail in venta.InvoiceDetails)
                                {
                                    entry.Detalles.Add(new Detalle
                                    {
                                        InvoiceNr = invoiceDetail.InvoiceNumber,
                                        Sum = invoiceDetail.Amount.ToString("F2"),
                                        Objects = $"Tarjeta Crédito - {transactionId} - {authNumber}",
                                        Stp = "1"
                                    });
                                    _logger.LogInformation($"Agregando factura Cobalt {invoiceDetail.InvoiceNumber} con monto {invoiceDetail.Amount:F2}");
                                }
                                
                                var datos = new Dictionary<string, Entry> { { entry.SerNr, entry } };
                                
                                // Registrar el recibo en Hansa
                                using (var scope = HttpContext.RequestServices.CreateScope())
                                {
                                    var hansaReceiptService = scope.ServiceProvider.GetService<IHansaReceiptService>();
                                    var logger = scope.ServiceProvider.GetService<ILogger<ClientesController>>();
                                    
                                    string? serNrRegistrado = await AgregarRecibosDeCajasAsync(datos, _hansaSettings, logger, hansaReceiptService);
                                    
                                    if (!string.IsNullOrEmpty(serNrRegistrado) && serNrRegistrado != "20878" && serNrRegistrado != "20060")
                                    {
                                        _logger.LogInformation("Recibo Cobalt registrado exitosamente en Hansa. SerNr: {SerNr}", serNrRegistrado);
                                        
                                        // Enviar notificación usando el método reutilizable
                                        if (!string.IsNullOrEmpty(venta.customer_email))
                                        {
                                            await EnviarNotificacionReciboAsync(
                                                serNrRegistrado,
                                                venta.customer_email,
                                                "CREDITCARD", 
                                                venta.InvoiceDetails.Sum(d => d.Amount),
                                                string.Join(", ", venta.InvoiceDetails.Select(d => d.InvoiceNumber)),
                                                venta.contract_number ?? "CARD_CLIENT",
                                                venta.customer_name ?? venta.card_holder ?? "Cliente Tarjeta"
                                            );
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogError("Error registrando recibo Cobalt en Hansa. Código: {Codigo}", serNrRegistrado);
                                    }
                                }
                            }
                            catch (Exception hansaEx)
                            {
                                _logger.LogError(hansaEx, "Error registrando recibo Cobalt en Hansa");
                            }

                            // Información adicional
                            var clienteNombre = venta.customer_name ?? venta.card_holder ?? "Cliente";
                            var ordenId = venta.order_id ?? "N/A";
                            var descripcion = venta.description ?? "Pago con tarjeta de crédito";

                            // 3.1 Correo para el cliente
                            // Si no se proporcionó email, intentar obtenerlo de la base de datos local
                            var emailCliente = venta.customer_email;
                            
                            if (string.IsNullOrEmpty(emailCliente) && !string.IsNullOrEmpty(venta.contract_number))
                            {
                                try
                                {
                                    using (var scope = HttpContext.RequestServices.CreateScope())
                                    {
                                        var db = scope.ServiceProvider.GetRequiredService<ReciboCajaOfflineContext>();
                                        var clienteLocal = await db.ClientesLocales
                                            .FirstOrDefaultAsync(c => c.Code == venta.contract_number);
                                        
                                        if (clienteLocal != null && !string.IsNullOrEmpty(clienteLocal.eMail))
                                        {
                                            emailCliente = clienteLocal.eMail;
                                            _logger.LogInformation("Email del cliente obtenido de base de datos local: {Email} para código: {Code}", 
                                                emailCliente, venta.contract_number);
                                        }
                                        else
                                        {
                                            _logger.LogWarning("No se encontró email en base de datos local para cliente: {Code}", 
                                                venta.contract_number);
                                        }
                                    }
                                }
                                catch (Exception dbEx)
                                {
                                    _logger.LogError(dbEx, "Error al buscar email del cliente en base de datos local");
                                }
                            }
                            
                            // NOTA: El email al cliente ahora se envía desde EnviarNotificacionReciboAsync con la plantilla corporativa correcta
                            // Este bloque se comentó para evitar emails duplicados con plantilla incorrecta
                            if (!string.IsNullOrEmpty(emailCliente))
                            {
                                _logger.LogInformation("Email al cliente Tarjeta ya enviado por EnviarNotificacionReciboAsync via endpoint /recibos");
                            }

                            // 3.2 Correo para administrador/empresa
                            try
                            {
                                var asuntoAdmin = "💰 Nueva Venta con Tarjeta de Crédito - Celero Network";
                                var numeroContrato = !string.IsNullOrEmpty(venta.contract_number) ? venta.contract_number : venta.order_id ?? ordenId;
                                var cuerpoAdmin = GenerarCorreoAdministradorTarjeta(clienteNombre, emailCliente, fechaPago, monto, moneda, transactionId, authNumber, ordenId, descripcion, numeroContrato);

                                var correoAdminEnviado = await EnviarCorreoInterno(
                                    "gs@celero.net", // Cambiar por el correo real de notificaciones
                                    asuntoAdmin,
                                    cuerpoAdmin
                                );

                                if (correoAdminEnviado)
                                {
                                    _logger.LogInformation("Notificación de venta enviada al administrador");
                                }
                                else
                                {
                                    _logger.LogWarning("No se pudo enviar notificación al administrador");
                                }
                            }
                            catch (Exception adminEmailEx)
                            {
                                _logger.LogError(adminEmailEx, "Error enviando notificación al administrador");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("No se enviaron correos de notificación. Estado general: '{estadoGeneral}', Estado transacción: '{estadoTransaccion}' - Se requiere status='ok' y data.status='authorized'",
                                estadoGeneral, estadoTransaccion);
                        }
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Error en proceso asíncrono de envío de correos");
                    }
                });

                // Responder INMEDIATAMENTE con el resultado de Cobalt
                // El envío de correos continúa en segundo plano
                return Content(ventaResult, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando venta con tarjeta");
                return StatusCode(500, new { error = "Error interno procesando la venta", message = ex.Message });
            }
        }
        [HttpPost("yappy/crear-orden")]
        public async Task<IActionResult> CrearOrdenYappy([FromBody] YappyOrdenRequest req)
        {
            try
            {
                // 1. Validar comercio y obtener token
                var validarBody = new
                {
                    merchantId = _yappySettings.MerchantId,
                    urlDomain = req.domain
                };

                var http = new HttpClient();
                var validarResp = await http.PostAsync(
                    _yappySettings.ValidateMerchantUrl,
                    new StringContent(System.Text.Json.JsonSerializer.Serialize(validarBody), Encoding.UTF8, "application/json")
                );
                if (!validarResp.IsSuccessStatusCode)
                {
                    _logger.LogError("Error validando merchant Yappy: {StatusCode}", validarResp.StatusCode);
                    return StatusCode((int)validarResp.StatusCode, await validarResp.Content.ReadAsStringAsync());
                }

                var validarJson = await validarResp.Content.ReadAsStringAsync();
                var validarObj = System.Text.Json.JsonSerializer.Deserialize<YappyValidarResponse>(validarJson);
                var token = validarObj?.body?.token;

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("Token de sesión Yappy no recibido");
                    return StatusCode(500, "Token de sesión no recibido");
                }

                // 2. Crear orden
                var ordenBody = new
                {
                    merchantId = _yappySettings.MerchantId,
                    orderId = req.orderId,
                    domain = req.domain,
                    paymentDate = req.paymentDate,
                    aliasYappy = req.aliasYappy,
                    ipnUrl = req.ipnUrl,
                    discount = req.discount,
                    taxes = req.taxes,
                    subtotal = req.subtotal,
                    total = req.total
                };

                var ordenHttp = new HttpClient();
                ordenHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token);
                var ordenResp = await ordenHttp.PostAsync(
                    _yappySettings.CreateOrderUrl,
                    new StringContent(System.Text.Json.JsonSerializer.Serialize(ordenBody), Encoding.UTF8, "application/json")
                );

                var ordenJson = await ordenResp.Content.ReadAsStringAsync();

                if (!ordenResp.IsSuccessStatusCode)
                {
                    _logger.LogError("Error creando orden Yappy: {StatusCode} - {Response}", ordenResp.StatusCode, ordenJson);
                }
                else
                {
                    _logger.LogInformation("Orden Yappy creada exitosamente");
                    
                    // Almacenar información de facturas para usar en el IPN
                    if (req.InvoiceDetails != null && req.InvoiceDetails.Count > 0)
                    {
                        try
                        {
                            using (var scope = HttpContext.RequestServices.CreateScope())
                            {
                                var db = scope.ServiceProvider.GetRequiredService<ReciboCajaOfflineContext>();
                                
                                var ordenInfo = new Models.ReciboCajaOffline
                                {
                                    SerNr = req.orderId, // Usar orderId como SerNr
                                    TransDate = DateTime.Now,
                                    PayMode = "YP_PENDING", // Marcado como pendiente
                                    Person = req.ClienteCode,
                                    CUCode = req.ClienteCode,
                                    RefStr = req.orderId,
                                    DetallesJson = System.Text.Json.JsonSerializer.Serialize(new 
                                    { 
                                        OrderId = req.orderId,
                                        InvoiceDetails = req.InvoiceDetails,
                                        ClienteCode = req.ClienteCode,
                                        EmailCliente = req.EmailCliente,
                                        AliasYappy = req.aliasYappy,
                                        Total = req.total,
                                        MetodoPago = "Yappy",
                                        FechaCreacion = DateTime.Now
                                    }),
                                    Pendiente = true
                                };
                                
                                db.RecibosOffline.Add(ordenInfo);
                                await db.SaveChangesAsync();
                                
                                _logger.LogInformation("Información de orden Yappy almacenada para IPN. OrderId: {OrderId}, Facturas: {Count}", 
                                    req.orderId, req.InvoiceDetails.Count);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error almacenando información de orden Yappy: {OrderId}", req.orderId);
                            // No fallar la creación de orden por esto
                        }
                    }
                }

                return Content(ordenJson, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Yappy");
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost("yappy/crear-orden-frontend")]
        public async Task<IActionResult> CrearOrdenYappyFrontend([FromBody] YappyFrontendRequest req)
        {
            try
            {
                // Validar InvoiceDetails
                if (req.InvoiceDetails == null || req.InvoiceDetails.Count == 0)
                {
                    _logger.LogError("ATENCIÓN: InvoiceDetails es requerido para Yappy");
                    return BadRequest(new { success = false, message = "InvoiceDetails es requerido con el número de factura y monto específico para cada factura." });
                }

                var orderId = Guid.NewGuid().ToString();
                string domain = _configuration["App:BaseUrl"] ?? "https://selfservice-dev.celero.network";
                long paymentDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string ipnUrl = $"{_configuration["App:ApiUrl"] ?? "https://localhost:7262"}/api/clientes/yappy/ipn";
                
                // Calcular total desde InvoiceDetails
                decimal totalAmount = req.InvoiceDetails.Sum(d => d.Amount);
                string subtotal = totalAmount.ToString("F2");
                string total = subtotal;

                var yappyReq = new YappyOrdenRequest
                {
                    orderId = orderId,
                    domain = domain,
                    paymentDate = paymentDate,
                    aliasYappy = req.yappyPhone,
                    ipnUrl = ipnUrl,
                    discount = "0.00",
                    taxes = "0.00", 
                    subtotal = subtotal,
                    total = total,
                    InvoiceDetails = req.InvoiceDetails,
                    ClienteCode = req.ClienteCode,
                    EmailCliente = req.EmailCliente
                };
                
                
                return await CrearOrdenYappy(yappyReq);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear orden Yappy frontend");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        [HttpPost("verificar-recaptcha")]
        public async Task<IActionResult> VerificarRecaptcha([FromBody] RecaptchaRequest recaptchaRequest)
        {
            if (recaptchaRequest == null || string.IsNullOrEmpty(recaptchaRequest.Token))
            {
                return BadRequest(new { success = false, message = "Token de reCAPTCHA es requerido." });
            }

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var projectId = _configuration["GoogleRecaptcha:ProjectId"]; // Leer desde appsettings.json
                var apiKey = _configuration["GoogleRecaptcha:ApiKey"]; // Leer desde appsettings.json
                var siteKey = _configuration["GoogleRecaptcha:SiteKey"]; // Leer desde appsettings.json

                if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(siteKey))
                {
                    _logger.LogError("Configuración de Google reCAPTCHA incompleta en appsettings.json.");
                    return StatusCode(500, new { success = false, message = "Error de configuración del servidor para reCAPTCHA." });
                }

                var url = $"https://recaptchaenterprise.googleapis.com/v1/projects/{projectId}/assessments?key={apiKey}";

                var payload = new GoogleRecaptchaVerificationRequest
                {
                    Event = new EventPayload
                    {
                        Token = recaptchaRequest.Token,
                        SiteKey = siteKey,
                        // ExpectedAction = "USER_ACTION" // Opcional: si definiste acciones en el frontend
                    }
                }; var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                // Imprimir el JSON para depuración
                _logger.LogInformation($"JSON enviado a Google: {jsonPayload}");
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Error de la API de Google reCAPTCHA: {response.StatusCode} - {responseString}");
                    return StatusCode((int)response.StatusCode, new { success = false, message = "Error al verificar el token de reCAPTCHA.", details = responseString });
                }

                var verificationResponse = System.Text.Json.JsonSerializer.Deserialize<GoogleRecaptchaVerificationResponse>(responseString);

                // Aquí decides el umbral de puntuación y si la acción es válida
                // Por ejemplo, si la puntuación es > 0.5 y el token es válido
                bool isTokenValid = verificationResponse?.TokenProperties?.Valid ?? false;
                float score = verificationResponse?.RiskAnalysis?.Score ?? 0.0f;
                // string action = verificationResponse?.TokenProperties?.Action; // Si usas acciones

                // Lógica de decisión basada en la puntuación y validez del token
                // Este es un ejemplo, ajusta según tus necesidades de seguridad
                if (isTokenValid && score >= 0.5) // Umbral de ejemplo
                {
                    // El usuario es probablemente legítimo
                    return Ok(new { success = true, message = "Verificación de reCAPTCHA exitosa.", score = score });
                }
                else
                {
                    // El usuario es sospechoso o el token no es válido
                    _logger.LogWarning($"Verificación de reCAPTCHA fallida o puntuación baja: {score}. Token válido: {isTokenValid}");
                    return Ok(new { success = false, message = "Falló la verificación de reCAPTCHA o la puntuación es demasiado baja.", score = score, tokenValid = isTokenValid });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error interno durante la verificación de reCAPTCHA.");
                return StatusCode(500, new { success = false, message = $"Error interno del servidor: {ex.Message}" });
            }
        }

        #region Email Endpoints

        [HttpPost("email/send")]
        public async Task<IActionResult> SendEmail([FromBody] EmailRequest emailRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Datos de email inválidos", errors = ModelState });
            }

            try
            {
                var result = await _emailService.SendEmailAsync(emailRequest);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar email");
                return StatusCode(500, new EmailResponse
                {
                    Success = false,
                    Error = $"Error interno del servidor: {ex.Message}"
                });
            }
        }

        [HttpPost("email/payment-confirmation")]
        public async Task<IActionResult> SendPaymentConfirmation([FromBody] PaymentConfirmationEmailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Datos inválidos", errors = ModelState });
            }

            try
            {
                var result = await _emailService.SendPaymentConfirmationAsync(request);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar confirmación de pago");
                return StatusCode(500, new EmailResponse
                {
                    Success = false,
                    Error = $"Error interno del servidor: {ex.Message}"
                });
            }
        }

        [HttpPost("email/invoice-reminder")]
        public async Task<IActionResult> SendInvoiceReminder([FromBody] InvoiceReminderEmailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Datos inválidos", errors = ModelState });
            }

            try
            {
                var result = await _emailService.SendInvoiceReminderAsync(request);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar recordatorio de factura");
                return StatusCode(500, new EmailResponse
                {
                    Success = false,
                    Error = $"Error interno del servidor: {ex.Message}"
                });
            }
        }

        [HttpPost("email/welcome")]
        public async Task<IActionResult> SendWelcomeEmail([FromBody] WelcomeEmailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Datos inválidos", errors = ModelState });
            }

            try
            {
                var result = await _emailService.SendWelcomeEmailAsync(request);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar email de bienvenida");
                return StatusCode(500, new EmailResponse
                {
                    Success = false,
                    Error = $"Error interno del servidor: {ex.Message}"
                });
            }
        }

        #endregion

        /// <summary>
        /// IPN (Instant Payment Notification) de Yappy según documentación oficial
        /// </summary>
        [HttpGet("yappy/ipn")]
        public async Task<IActionResult> YappyIPN(
            [FromQuery] string orderId,
            [FromQuery] string status,
            [FromQuery] string hash,
            [FromQuery] string domain,
            [FromQuery] string confirmationNumber = "")
        {
            try
            {
                _logger.LogInformation("IPN Yappy recibido - OrderId: {OrderId}, Status: {Status}, Domain: {Domain}, ConfirmationNumber: {ConfirmationNumber}", 
                    orderId, status, domain, confirmationNumber);

                // Validar parámetros requeridos
                if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(status) || 
                    string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(domain))
                {
                    _logger.LogWarning("IPN Yappy: parámetros faltantes");
                    return Ok(new { success = false, message = "Parámetros faltantes" });
                }

                // Verificar hash según documentación oficial
                if (!VerifyYappyHash(orderId, status, domain, hash))
                {
                    _logger.LogWarning("IPN Yappy: hash inválido para orderId {OrderId}", orderId);
                    return Ok(new { success = false, message = "Hash inválido" });
                }

                // Procesar según el estado
                switch (status)
                {
                    case "E": // Ejecutado - El cliente confirmó el pago y se completó la compra
                        try
                        {
                            _logger.LogInformation("Procesando pago Yappy exitoso. OrderId: {OrderId}", orderId);
                            
                            // 1. Registrar recibo en Hansa
                            await RegistrarReciboYappyOffline(orderId);
                            
                            _logger.LogInformation("Pago Yappy procesado exitosamente. OrderId: {OrderId}", orderId);
                            
                            return Ok(new { success = true, confirmation = confirmationNumber });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error procesando pago Yappy exitoso. OrderId: {OrderId}", orderId);
                            // Aún retornar éxito para Yappy pero log el error
                            return Ok(new { success = true, confirmation = confirmationNumber, note = "Processed with errors" });
                        }

                    case "R": // Rechazado - Cliente no confirmó el pago dentro de los cinco minutos
                        _logger.LogInformation("Pago Yappy rechazado. OrderId: {OrderId}", orderId);
                        return Ok(new { success = true, status = "rejected" });

                    case "C": // Cancelado - Cliente canceló el pedido en la app
                        _logger.LogInformation("Pago Yappy cancelado. OrderId: {OrderId}", orderId);
                        return Ok(new { success = true, status = "cancelled" });

                    case "X": // Expirado - Cliente no inició el proceso de pago y la solicitud expiró
                        _logger.LogInformation("Pago Yappy expirado. OrderId: {OrderId}", orderId);
                        return Ok(new { success = true, status = "expired" });

                    default:
                        _logger.LogWarning("Estado Yappy desconocido: {Status} para OrderId: {OrderId}", status, orderId);
                        return Ok(new { success = false, message = "Estado desconocido" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando IPN Yappy. OrderId: {OrderId}", orderId);
                return Ok(new { success = false, message = "Error interno" });
            }
        }

        /// <summary>
        /// Verifica el hash de Yappy según la documentación oficial
        /// </summary>
        private bool VerifyYappyHash(string orderId, string status, string domain, string hash)
        {
            try
            {
                // Obtener clave secreta desde configuración o variable de entorno
                var secretKey = _configuration["Yappy:SecretKey"] ?? 
                               Environment.GetEnvironmentVariable("YAPPY_SECRET_KEY") ??
                               "WVBfQTNBQjA1NkYtRkNFOS0zMjgzLUE4NEQtOTUzRkZFRTQ5MzkzLmRlNjRhY2IwLWVlNDktNDMyOS1hZGU0LThlNTE1MDc5OGFlMA==";

                return hash.Equals(CreateYappyHash(orderId + status + domain, secretKey), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando hash de Yappy");
                return false;
            }
        }

        /// <summary>
        /// Crea hash HMAC-SHA256 según documentación oficial de Yappy
        /// </summary>
        private string CreateYappyHash(string data, string secretKey)
        {
            try
            {
                var bytes = Convert.FromBase64String(secretKey);
                var secret = Encoding.UTF8.GetString(bytes);

                string[] secrets = secret.Split('.');
                if (secrets.Length == 0) return string.Empty;

                var keyBytes = Encoding.UTF8.GetBytes(secrets[0]);
                using (var hmacsha256 = new System.Security.Cryptography.HMACSHA256(keyBytes))
                {
                    var hashBytes = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando hash de Yappy");
                return string.Empty;
            }
        }
        [HttpGet("health")]
        public IActionResult Health()
        {
            // Este endpoint debe ser rápido y confiable para evitar timeouts 
            // que el frontend puede interpretar como errores de CORS.
            // La sincronización pesada se debe invocar con el endpoint /sincronizar-clientes.
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "Api_Celero"
            });
        }

        [HttpPost("sincronizar-clientes")]
        public async Task<IActionResult> SincronizarClientes()
        {
            try
            {
                var result = await EjecutarSincronizacionInterna();

                // Convertir el resultado a un dictionary para acceder a sus propiedades
                var resultJson = JsonSerializer.Serialize(result);
                var resultObj = JsonSerializer.Deserialize<Dictionary<string, object>>(resultJson);

                if (resultObj != null && resultObj.ContainsKey("success"))
                {
                    var success = resultObj["success"].ToString() == "True";
                    if (success)
                    {
                        return Ok(result);
                    }
                    else
                    {
                        return BadRequest(result);
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la sincronización de clientes");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error interno durante la sincronización: {ex.Message}"
                });
            }
        }
        [HttpGet("clientes-locales")]
        public async Task<IActionResult> ObtenerClientesLocales([FromQuery] string? buscar = null, [FromQuery] string? vatNr = null, [FromQuery] string? mobile = null)
        {
            try
            {
                using (var scope = HttpContext.RequestServices.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ReciboCajaOfflineContext>();

                    var query = db.ClientesLocales.AsQueryable();

                    // Búsqueda general por código, nombre, email o VATNr
                    if (!string.IsNullOrEmpty(buscar))
                    {
                        query = query.Where(c =>
                            c.Code.Contains(buscar) ||
                            c.Name.Contains(buscar) ||
                            c.eMail.Contains(buscar) ||
                            c.VATNr.Contains(buscar));
                    }

                    // Búsqueda específica por VATNr (RUC/Cédula)
                    if (!string.IsNullOrEmpty(vatNr))
                    {
                        query = query.Where(c => c.VATNr.Contains(vatNr));
                    }

                    // Búsqueda específica por Mobile (teléfono)
                    if (!string.IsNullOrEmpty(mobile))
                    {
                        query = query.Where(c => c.Mobile.Contains(mobile));
                    }

                    var clientes = await query.OrderBy(c => c.Code).ToListAsync();

                    return Ok(new
                    {
                        success = true,
                        data = clientes,
                        total = clientes.Count,
                        filtros = new
                        {
                            buscar = buscar,
                            vatNr = vatNr,
                            mobile = mobile
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener clientes locales");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error interno: {ex.Message}"
                });
            }
        }

        [HttpGet("buscar")]
        public async Task<IActionResult> BuscarClientes([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest(new { success = false, message = "El parámetro 'query' es requerido" });
                }

                using (var scope = HttpContext.RequestServices.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ReciboCajaOfflineContext>();

                    // Buscar en múltiples campos incluyendo VATNr para RUCs
                    var clientes = await db.ClientesLocales
                        .Where(c =>
                            c.Code.Contains(query) ||
                            c.Name.Contains(query) ||
                            c.eMail.Contains(query) ||
                            c.VATNr.Contains(query) ||
                            c.Mobile.Contains(query))
                        .OrderBy(c => c.Code)
                        .ToListAsync();

                    return Ok(clientes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar clientes con query: {Query}", query);
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error interno: {ex.Message}"
                });
            }
        }

        /// <summary>
                 /// Limpia y normaliza números telefónicos
                 /// - Quita códigos de país (+505, +507, etc.)
                 /// - Quita separadores como guiones (-), espacios, paréntesis
                 /// - Devuelve números de 8 dígitos (Nicaragua) o 8 dígitos (Panamá)
                 /// - Maneja números de 7 dígitos agregando un cero al inicio
                 /// </summary>
        private string LimpiarNumeroTelefonico(string numeroOriginal)
        {
            if (string.IsNullOrWhiteSpace(numeroOriginal))
                return string.Empty;

            string numeroLimpio = numeroOriginal.Trim();

            // Quitar códigos de país comunes con formato +XXX
            string[] codigosPais = { "+505", "+507", "+1", "+506", "+52" };
            foreach (string codigo in codigosPais)
            {
                if (numeroLimpio.StartsWith(codigo))
                {
                    numeroLimpio = numeroLimpio.Substring(codigo.Length);
                    break;
                }
            }

            // Quitar códigos de país con paréntesis (XXX)
            string[] codigosPaisParentesis = { "(505)", "(507)", "(1)", "(506)", "(52)" };
            foreach (string codigo in codigosPaisParentesis)
            {
                if (numeroLimpio.StartsWith(codigo))
                {
                    numeroLimpio = numeroLimpio.Substring(codigo.Length);
                    break;
                }
            }

            // Quitar códigos de país sin el signo + al inicio del número
            string[] codigosPaisSinMas = { "505", "507" };
            foreach (string codigo in codigosPaisSinMas)
            {
                if (numeroLimpio.StartsWith(codigo) && numeroLimpio.Length > codigo.Length + 6) // Debe tener al menos 6 dígitos más después del código
                {
                    numeroLimpio = numeroLimpio.Substring(codigo.Length);
                    break;
                }
            }

            // Quitar todos los separadores (guiones, espacios, paréntesis, puntos, etc.)
            numeroLimpio = System.Text.RegularExpressions.Regex.Replace(numeroLimpio, @"[^\d]", "");

            // Validar y normalizar longitud
            if (numeroLimpio.Length == 8 && numeroLimpio.All(char.IsDigit))
            {
                return numeroLimpio;
            }
            else if (numeroLimpio.Length == 7 && numeroLimpio.All(char.IsDigit))
            {
                // Para números de 7 dígitos, agregar un 0 al inicio (común en algunos países)
                return "0" + numeroLimpio;
            }
            else if (numeroLimpio.Length == 10 && numeroLimpio.All(char.IsDigit))
            {
                // Para números de 10 dígitos, tomar los últimos 8 (quitar área code)
                return numeroLimpio.Substring(2);
            }

            // Si no se puede normalizar, devolver el número original
            return numeroOriginal;
        }

        /// <summary>
        /// Ejecuta la sincronización de clientes internamente (usado por health check y endpoint manual)
        /// </summary>
        private async Task<object> EjecutarSincronizacionInterna()
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(_hansaSettings.TimeoutSeconds);

            // Configurar autenticación básica usando la configuración
            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_hansaSettings.Usuario}:{_hansaSettings.Clave}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Llamar a la API externa usando la configuración
            string apiUrl = $"{_hansaSettings.GetFullBaseUrl()}/api/{_hansaSettings.CompanyCode}/CUVc?fields=Code,Name,Mobile,eMail,VatNr,Closed";
            var response = await httpClient.GetAsync(apiUrl);

            if (!response.IsSuccessStatusCode)
            {
                return new
                {
                    success = false,
                    message = "Error al obtener datos de la API externa",
                    statusCode = (int)response.StatusCode
                };
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var clientesApi = JsonSerializer.Deserialize<ClienteApiResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (clientesApi?.data?.CUVc == null)
            {
                return new
                {
                    success = false,
                    message = "No se pudieron deserializar los datos de la API"
                };
            }

            // Sincronizar con SQLite
            using (var scope = HttpContext.RequestServices.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ReciboCajaOfflineContext>();

                // Verificar la fecha de la última actualización
                var ultimaActualizacion = await db.ClientesLocales
                    .OrderByDescending(c => c.FechaActualizacion)
                    .Select(c => c.FechaActualizacion)
                    .FirstOrDefaultAsync();

                // Si hay datos y la última actualización fue hace menos de 1 hora, no actualizar
                if (ultimaActualizacion != default(DateTime))
                {
                    var tiempoTranscurrido = DateTime.UtcNow - ultimaActualizacion;
                    if (tiempoTranscurrido.TotalHours < 1)
                    {
                        return new
                        {
                            success = false,
                            message = $"Sincronización reciente. Última actualización hace {tiempoTranscurrido.TotalMinutes:F1} minutos.",
                            ultimaActualizacion = ultimaActualizacion,
                            tiempoTranscurrido = new
                            {
                                minutos = (int)tiempoTranscurrido.TotalMinutes,
                                siguienteActualizacion = ultimaActualizacion.AddHours(1)
                            }
                        };
                    }
                }

                // Limpiar tabla existente
                db.ClientesLocales.RemoveRange(db.ClientesLocales);

                // Agregar nuevos datos
                foreach (var clienteApi in clientesApi.data.CUVc)
                {
                    var clienteLocal = new ClienteLocal
                    {
                        Code = clienteApi.Code,
                        Name = clienteApi.Name,
                        VATNr = clienteApi.VATNr,
                        eMail = clienteApi.eMail,
                        Mobile = LimpiarNumeroTelefonico(clienteApi.Mobile),
                        Closed = clienteApi.Closed,
                        FechaActualizacion = DateTime.UtcNow
                    };
                    db.ClientesLocales.Add(clienteLocal);
                }

                await db.SaveChangesAsync();
            }

            return new
            {
                success = true,
                message = $"Sincronización completada. {clientesApi.data.CUVc.Count} clientes sincronizados.",
                cantidad = clientesApi.data.CUVc.Count,
                timestamp = DateTime.UtcNow,
                proximaActualizacionPermitida = DateTime.UtcNow.AddHours(1)
            };
        }

        /// <summary>
        /// Procesa el parámetro range según las reglas de negocio:
        /// - Si tiene 5 dígitos numéricos: anteponer "CU-"
        /// - Si empieza con "CU-": dejarlo tal como está
        /// - Si tiene 8 dígitos: buscar en tabla local por Mobile y devolver Code
        /// - Otros casos: buscar en tabla local por VATNr y devolver Code
        /// </summary>
        private async Task<string> ProcesarRangeParameter(string range)
        {
            if (string.IsNullOrWhiteSpace(range))
                return ""; // Valor por defecto

            range = range.Trim();

            // Si ya empieza con "CU-", es un código válido
            if (range.StartsWith("CU-", StringComparison.OrdinalIgnoreCase))
                return range;

            // Si tiene exactamente 5 dígitos, anteponer "CU-"
            if (range.Length == 5 && range.All(char.IsDigit))
                return $"CU-{range}";

            // Si tiene exactamente 8 dígitos, buscar por Mobile en tabla local
            if (range.Length == 8 && range.All(char.IsDigit))
            {
                return await BuscarCodigoPorMobile(range);
            }

            // Para otros casos, buscar por VATNr en tabla local
            return await BuscarCodigoPorVATNr(range);
        }

        /// <summary>
        /// Busca un cliente por número de móvil en la tabla local y devuelve su Code
        /// </summary>
        private async Task<string> BuscarCodigoPorMobile(string mobile)
        {
            try
            {
                using (var scope = HttpContext.RequestServices.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ReciboCajaOfflineContext>();

                    // Limpiar el número de entrada usando la misma lógica que en la sincronización
                    // string mobileNormalizado = LimpiarNumeroTelefonico(mobile);
                    string mobileNormalizado = mobile;
                    // Buscar primero por coincidencia exacta con el número normalizado
                    var cliente = await db.ClientesLocales
                        .Where(c => c.Mobile == mobileNormalizado)
                        .FirstOrDefaultAsync();

                    // Si no encuentra con el número normalizado, buscar por el número original
                    if (cliente == null)
                    {
                        cliente = await db.ClientesLocales
                            .Where(c => c.Mobile == mobile)
                            .FirstOrDefaultAsync();
                    }

                    // Si aún no encuentra, buscar por coincidencia parcial (contiene)
                    if (cliente == null && mobileNormalizado.Length >= 8)
                    {
                        cliente = await db.ClientesLocales
                            .Where(c => c.Mobile.Contains(mobileNormalizado) || mobileNormalizado.Equals(c.Mobile))
                            .FirstOrDefaultAsync();
                    }

                    return cliente?.Code ?? mobile; // Si no encuentra, devolver el mobile original
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al buscar cliente por mobile: {mobile}", mobile);
                return mobile; // En caso de error, devolver el valor original
            }
        }

        /// <summary>
        /// Busca un cliente por VATNr en la tabla local y devuelve su Code
        /// </summary>
        private async Task<string> BuscarCodigoPorVATNr(string vatNr)
        {
            try
            {
                using (var scope = HttpContext.RequestServices.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ReciboCajaOfflineContext>();

                    //var cliente = await db.ClientesLocales
                    //    .Where(c => c.VATNr == vatNr)
                    //    .FirstOrDefaultAsync();
                    var cliente = await db.ClientesLocales
                 .Where(c => c.VATNr.StartsWith(vatNr))
                 .FirstOrDefaultAsync();

                    return cliente?.Code ?? vatNr; // Si no encuentra, devolver el vatNr original
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al buscar cliente por VATNr: {vatNr}", vatNr);
                return vatNr; // En caso de error, devolver el valor original
            }
        }

        /// <summary>
        /// Endpoint para obtener las empresas disponibles para selección en el frontend
        /// </summary>
        [HttpGet("empresas")]
        public IActionResult ObtenerEmpresas()
        {
            try
            {
                var empresasDisponibles = _hansaSettings.Companies
                    .Where(c => c.ActiveStatus == "Activo")
                    .Select(c => new {
                        compCode = c.CompCode,
                        compName = c.CompName,
                        shortName = c.ShortName,
                        activeStatus = c.ActiveStatus,
                        isDefault = c.CompCode == _hansaSettings.CompanyCode
                    })
                    .OrderBy(c => c.compCode)
                    .ToList();

                return Ok(new {
                    success = true,
                    message = $"Se encontraron {empresasDisponibles.Count} empresas activas",
                    data = empresasDisponibles,
                    meta = new {
                        total = empresasDisponibles.Count,
                        defaultCompany = _hansaSettings.CompanyCode,
                        timestamp = DateTime.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener empresas disponibles");
                return StatusCode(500, new {
                    success = false,
                    message = "Error interno del servidor",
                    data = new List<object>(),
                    meta = new {
                        total = 0,
                        error = ex.Message,
                        timestamp = DateTime.UtcNow
                    }
                });
            }
        }

        /// <summary>
        /// Endpoint para obtener períodos de fechas predefinidos para facilitar la selección
        /// </summary>
        [HttpGet("periodos-fechas")]
        public IActionResult ObtenerPeriodosFechas()
        {
            try
            {
                var hoy = DateTime.Now;
                var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
                var inicioMesAnterior = inicioMes.AddMonths(-1);
                var finMesAnterior = inicioMes.AddDays(-1);
                var inicioAno = new DateTime(hoy.Year, 1, 1);
                var inicioAnoAnterior = new DateTime(hoy.Year - 1, 1, 1);
                var finAnoAnterior = new DateTime(hoy.Year - 1, 12, 31);

                var periodos = new List<object>
                {
                    new {
                        id = "hoy",
                        nombre = "Hoy",
                        descripcion = "Solo el día de hoy",
                        fechaInicio = hoy.ToString("yyyy-MM-dd"),
                        fechaFin = hoy.ToString("yyyy-MM-dd"),
                        categoria = "reciente"
                    },
                    new {
                        id = "ayer",
                        nombre = "Ayer",
                        descripcion = "Solo el día de ayer",
                        fechaInicio = hoy.AddDays(-1).ToString("yyyy-MM-dd"),
                        fechaFin = hoy.AddDays(-1).ToString("yyyy-MM-dd"),
                        categoria = "reciente"
                    },
                    new {
                        id = "ultimos7dias",
                        nombre = "Últimos 7 días",
                        descripcion = "Desde hace una semana hasta hoy",
                        fechaInicio = hoy.AddDays(-7).ToString("yyyy-MM-dd"),
                        fechaFin = hoy.ToString("yyyy-MM-dd"),
                        categoria = "reciente"
                    },
                    new {
                        id = "ultimos30dias",
                        nombre = "Últimos 30 días",
                        descripcion = "Desde hace un mes hasta hoy",
                        fechaInicio = hoy.AddDays(-30).ToString("yyyy-MM-dd"),
                        fechaFin = hoy.ToString("yyyy-MM-dd"),
                        categoria = "reciente"
                    },
                    new {
                        id = "mesActual",
                        nombre = "Este mes",
                        descripcion = $"{inicioMes:MMMM yyyy} (completo)",
                        fechaInicio = inicioMes.ToString("yyyy-MM-dd"),
                        fechaFin = hoy.ToString("yyyy-MM-dd"),
                        categoria = "mensual"
                    },
                    new {
                        id = "mesAnterior",
                        nombre = "Mes anterior",
                        descripcion = $"{inicioMesAnterior:MMMM yyyy} (completo)",
                        fechaInicio = inicioMesAnterior.ToString("yyyy-MM-dd"),
                        fechaFin = finMesAnterior.ToString("yyyy-MM-dd"),
                        categoria = "mensual"
                    },
                    new {
                        id = "anoActual",
                        nombre = "Este año",
                        descripcion = $"Año {hoy.Year} (desde enero)",
                        fechaInicio = inicioAno.ToString("yyyy-MM-dd"),
                        fechaFin = hoy.ToString("yyyy-MM-dd"),
                        categoria = "anual"
                    },
                    new {
                        id = "anoAnterior",
                        nombre = "Año anterior",
                        descripcion = $"Año {hoy.Year - 1} (completo)",
                        fechaInicio = inicioAnoAnterior.ToString("yyyy-MM-dd"),
                        fechaFin = finAnoAnterior.ToString("yyyy-MM-dd"),
                        categoria = "anual"
                    }
                };

                var categorias = periodos
                    .GroupBy(p => ((dynamic)p).categoria)
                    .Select(g => new {
                        categoria = g.Key,
                        periodos = g.ToList(),
                        cantidad = g.Count()
                    })
                    .ToList();

                return Ok(new {
                    success = true,
                    message = $"Se generaron {periodos.Count} períodos de fechas disponibles",
                    data = periodos,
                    categorias = categorias,
                    meta = new {
                        total = periodos.Count,
                        fechaActual = hoy.ToString("yyyy-MM-dd"),
                        timestamp = DateTime.UtcNow,
                        recomendado = "ultimos30dias" // Período recomendado por defecto
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar períodos de fechas");
                return StatusCode(500, new {
                    success = false,
                    message = "Error interno del servidor",
                    data = new List<object>(),
                    meta = new {
                        total = 0,
                        error = ex.Message,
                        timestamp = DateTime.UtcNow
                    }
                });
            }
        }        /// <summary>
                 /// Envía un correo electrónico usando el servicio configurado
                 /// </summary>
                 /// <param name="to">Correo destinatario</param>        /// <param name="subject">Asunto</param>
                 /// <param name="body">Cuerpo del mensaje (HTML)</param>
                 /// <param name="from">(Opcional) Remitente</param>
                 /// <returns>Task con true si fue exitoso, false si falló</returns>
        private async Task<bool> EnviarCorreoInterno(string to, string subject, string body, string? from = null)
        {
            try
            {
                var result = await _emailService.SendEmailAsync(new EmailRequest
                {
                    To = to,
                    Subject = subject,
                    HtmlContent = body,
                    From = from // Si es null, el servicio usará el default
                });
                return result.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando correo interno a {to}", to);
                return false;
            }
        }        /// <summary>
        /// Genera el HTML del correo de confirmación para el cliente - PLANTILLA UNIFICADA
        /// </summary>
        /// <param name="metodoPago">Método de pago: TARJETA DE CREDITO/DEBITO, PAYPAL, YAPPY, ACH</param>
        private string GenerarCorreoClienteUnificado(string nombreCliente, string fechaPago, string monto, string moneda,
            string transactionId, string authNumber, string ordenId, string descripcion, string numeroContrato, 
            string companyCode = "2", string metodoPago = "TARJETA DE CREDITO/DEBITO")
        {
            // Formatear la fecha para mostrar solo dd/mm/yyyy
            string fechaFormateada = fechaPago;
            if (DateTime.TryParse(fechaPago, out DateTime fecha))
            {
                fechaFormateada = fecha.ToString("dd/MM/yyyy");
            }

            // Formatear el monto - quitar símbolo de moneda si existe
            string montoFormateado = monto.Replace("$", "").Replace("USD", "").Replace("US$", "").Trim();
            
            // Obtener el logo según la empresa
            string logoUrl = GetCompanyLogoUrl(companyCode);

            
            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Confirmación de Pago - Celero Network</title>
</head>
<body style='margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #ffffff;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #ffffff; padding: 20px 0;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background-color: #ffffff; border: 1px solid #ddd;'>
                    <!-- Header con logo -->
                    <tr>
                        <td style='padding: 20px; text-align: center; border-bottom: 1px solid #ddd;'>
                            <img src='{logoUrl}' 
                                 alt='Logo Empresa' 
                                 width='250' 
                                 style='display: block; margin: 0 auto; height: auto;' />
                        </td>
                    </tr>
                    
                    <!-- Título principal -->
                    <tr>
                        <td style='padding: 30px 40px 20px 40px; text-align: left;'>
                            <h1 style='color: #333; font-size: 24px; font-weight: bold; margin: 0;'>¡Gracias por su pago!</h1>
                        </td>
                    </tr>
                    
                    <!-- Información del cliente y pago -->
                    <tr>
                        <td style='padding: 0 40px 20px 40px;'>
                            <p style='margin: 0 0 10px 0; font-size: 16px; color: #333;'>
                                Estimado. <strong style='color: #6c5ce7;'>{nombreCliente.ToUpper()}</strong>
                            </p>
                            <p style='margin: 0; font-size: 16px; color: #333;'>
                                Hemos aplicado su cuenta <strong>${montoFormateado}</strong> recibido el <strong>{fechaFormateada}</strong> mediante <strong>{metodoPago}</strong>.
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Tabla de detalle de cobro -->
                    <tr>
                        <td style='padding: 0 40px 30px 40px;'>
                            <div style='border: 2px solid #6c5ce7; padding: 20px;'>
                                <h3 style='color: #6c5ce7; margin: 0 0 15px 0; font-size: 18px; font-weight: bold;'>Detalle de Cobro</h3>
                                
                                <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse: collapse; font-size: 12px;'>
                                    <thead>
                                        <tr>
                                            <th style='padding: 8px; border: none; border-bottom: 1px solid #6c5ce7; text-align: left; font-weight: bold; color: #333; font-size: 11px;'>REFERENCIA</th>
                                            <th style='padding: 8px; border: none; border-bottom: 1px solid #6c5ce7; text-align: left; font-weight: bold; color: #333; font-size: 11px;'>Nro. CUFE</th>
                                            <th style='padding: 8px; border: none; border-bottom: 1px solid #6c5ce7; text-align: center; font-weight: bold; color: #333; font-size: 11px;'>CUOTA</th>
                                            <th style='padding: 8px; border: none; border-bottom: 1px solid #6c5ce7; text-align: right; font-weight: bold; color: #333; font-size: 11px;'>RECIBIDO</th>
                                            <th style='padding: 8px; border: none; border-bottom: 1px solid #6c5ce7; text-align: right; font-weight: bold; color: #333; font-size: 11px;'>SALDO</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr>
                                            <td style='padding: 8px; border: none; text-align: left; color: #333; font-size: 11px;'>{transactionId}</td>
                                            <td style='padding: 8px; border: none; text-align: left; color: #333; font-size: 11px;'>{numeroContrato}</td>
                                            <td style='padding: 8px; border: none; text-align: center; color: #333; font-size: 11px;'></td>
                                            <td style='padding: 8px; border: none; text-align: right; color: #333; font-size: 11px;'>{montoFormateado}</td>
                                            <td style='padding: 8px; border: none; text-align: right; color: #333; font-size: 11px;'>0.00</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Información de contacto -->
                    <tr>
                        <td style='padding: 0 40px 20px 40px;'>
                            <p style='margin: 0 0 5px 0; font-size: 14px; color: #333;'>
                                Si mantiene alguna consulta sobre la facturación, no dude en contactarnos respondiendo a esta notificación, llamando al <strong>+507 838-7575</strong> o a nuestro
                            </p>
                            <p style='margin: 0; font-size: 14px; color: #333;'>
                                <strong>Whatsapp</strong> haciendo <a href='https://api.whatsapp.com/send/?phone=50768587775' style='color: #6c5ce7; text-decoration: none;'><strong>clic aquí</strong></a>.
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Firma -->
                    <tr>
                        <td style='padding: 0 40px 30px 40px;'>
                            <p style='margin: 0; font-size: 14px; color: #333;'>Atentamente,</p>
                            <p style='margin: 5px 0 0 0; font-size: 14px; color: #333; font-weight: bold;'>Depto. Crédito y Cobros</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }
        // Métodos de compatibilidad - redirigen a la plantilla unificada
        private string GenerarCorreoClientePayPal(string nombreCliente, string fechaPago, string monto, string moneda,
            string transactionId, string authNumber, string ordenId, string descripcion, string numeroContrato, string empresa = "Celero Network", string recibo = "")
        {
            string companyCode = empresa.ToLower().Contains("quantum") ? "3" : "2";
            return GenerarCorreoClienteUnificado(nombreCliente, fechaPago, monto, moneda, transactionId, authNumber, ordenId, descripcion, numeroContrato, companyCode, "PAYPAL");
        }

        private string GenerarCorreoClienteYappy(string nombreCliente, string fechaPago, string monto, string moneda,
            string transactionId, string authNumber, string ordenId, string descripcion, string numeroContrato, string empresa = "Celero Network", string recibo = "")
        {
            string companyCode = empresa.ToLower().Contains("quantum") ? "3" : "2";
            return GenerarCorreoClienteUnificado(nombreCliente, fechaPago, monto, moneda, transactionId, authNumber, ordenId, descripcion, numeroContrato, companyCode, "YAPPY");
        }

        private string GenerarCorreoClienteACH(string nombreCliente, string fechaPago, string monto, string moneda,
            string transactionId, string authNumber, string ordenId, string descripcion, string numeroContrato, string empresa = "Celero Network", string recibo = "")
        {
            string companyCode = empresa.ToLower().Contains("quantum") ? "3" : "2";
            return GenerarCorreoClienteUnificado(nombreCliente, fechaPago, monto, moneda, transactionId, authNumber, ordenId, descripcion, numeroContrato, companyCode, "ACH");
        }

        /// <summary>
        /// Genera el HTML del correo de notificación para el administrador
        /// </summary>
        private string GenerarCorreoAdministrador(string nombreCliente, string? emailCliente, string fechaPago,
            string monto, string moneda, string transactionId, string authNumber, string ordenId, string descripcion, string numeroContrato)
        {
            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <meta http-equiv='X-UA-Compatible' content='IE=edge'>
                <title>Nueva Venta Registrada - Celero Network</title>
                <!--[if mso]>
                <noscript>
                    <xml>
                        <o:OfficeDocumentSettings>
                            <o:PixelsPerInch>96</o:PixelsPerInch>
                        </o:OfficeDocumentSettings>
                    </xml>
                </noscript>
                <![endif]-->
            </head>
            <body style='margin: 0; padding: 0; font-family: Arial, Helvetica, sans-serif; background-color: #f5f5f5; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%;'>
                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #f5f5f5;'>
                    <tr>
                        <td align='center' style='padding: 20px 0;'>
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='600' style='max-width: 600px; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                                <!-- Header con Logo -->
                                <tr>
                                    <td style='background-color: #ffffff; padding: 30px 20px; text-align: center; border-bottom: 3px solid #667eea; border-radius: 8px 8px 0 0;'>
                                        <!-- Contenedor para el logo con fallback -->
                                        <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                            <tr>
                                                <td align='center'>
                                                    <!-- Logo principal -->
                                                    <img src='https://selfservice-dev.celero.network/lovable-uploads/Cabecera.png' 
                                                         alt='CELERO NETWORK - Sistema de Pagos Seguro' 
                                                         title='Celero Network' 
                                                         width='250' 
                                                         height='auto' 
                                                         style='display: block; max-width: 250px; height: auto; margin: 0 auto; border: 0; outline: none; text-decoration: none;' />
                                                    <!-- Fallback text (siempre visible como respaldo) -->
                                                    <div style='font-size: 26px; font-weight: bold; color: #667eea; margin: 15px 0 5px 0; line-height: 1.2; mso-hide: all;'>
                                                        🌐 CELERO NETWORK
                                                    </div>
                                                    <div style='font-size: 14px; color: #666; margin: 0; mso-hide: all;'>
                                                        Sistema de Pagos Seguro
                                                    </div>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                
                                <!-- Título principal -->
                                <tr>
                                    <td style='background: linear-gradient(135deg, #ff7b7b 0%, #667eea 100%); padding: 30px 20px; text-align: center;'>
                                        <h1 style='color: white; margin: 0; font-size: 28px; font-weight: bold; line-height: 1.2;'>💰 Nueva Venta Registrada</h1>
                                        <p style='color: #f8f9fa; margin: 10px 0 0 0; font-size: 16px; line-height: 1.4;'>Pago con Tarjeta de Crédito</p>
                                    </td>
                                </tr>                                
                                <!-- Contenido principal -->
                                <tr>
                                    <td style='padding: 30px 20px; background-color: #f8f9fa;'>
                                        <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                            <tr>
                                                <td style='background-color: white; border-radius: 12px; padding: 25px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                                                    <h2 style='color: #333; margin-top: 0; font-size: 22px; margin-bottom: 15px;'>Detalles de la Transacción</h2>
                                                    
                                                    <!-- Información del Cliente -->
                                                    <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #e3f2fd; border-left: 4px solid #2196f3; border-radius: 0 8px 8px 0; margin: 20px 0;'>
                                                        <tr>
                                                            <td style='padding: 20px;'>
                                                                <h3 style='margin-top: 0; color: #1976d2; font-size: 18px; margin-bottom: 15px;'>👤 Información del Cliente</h3>
                                                                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                                                    <tr>
                                                                        <td style='padding: 8px 0; font-weight: bold; color: #555; width: 30%; vertical-align: top;'>Nombre:</td>
                                                                        <td style='padding: 8px 0; color: #333; vertical-align: top;'>{nombreCliente}</td>
                                                                    </tr>
                                                                    <tr>
                                                                        <td style='padding: 8px 0; font-weight: bold; color: #555; vertical-align: top;'>Email:</td>
                                                                        <td style='padding: 8px 0; color: #333; vertical-align: top;'>{emailCliente ?? "No proporcionado"}</td>
                                                                    </tr>
                                                                </table>
                                                            </td>
                                                        </tr>
                                                    </table>
                                                    
                                                    <!-- Información del Pago -->
                                                    <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #f3e5f5; border-left: 4px solid #9c27b0; border-radius: 0 8px 8px 0; margin: 20px 0;'>
                                                        <tr>
                                                            <td style='padding: 20px;'>
                                                                <h3 style='margin-top: 0; color: #7b1fa2; font-size: 18px; margin-bottom: 15px;'>💳 Información del Pago</h3>
                                                                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>                                                                    <tr>
                                                                        <td style='padding: 8px 0; font-weight: bold; color: #555; width: 30%; vertical-align: top;'>Orden:</td>
                                                                        <td style='padding: 8px 0; color: #333; vertical-align: top;'>{ordenId}</td>
                                                                    </tr>
                                                                    <tr>
                                                                        <td style='padding: 8px 0; font-weight: bold; color: #555; vertical-align: top;'>Número de Contrato:</td>
                                                                        <td style='padding: 8px 0; color: #333; font-family: monospace; font-size: 14px; vertical-align: top;'>{numeroContrato}</td>
                                                                    </tr>
                                                                    <tr>
                                                                        <td style='padding: 8px 0; font-weight: bold; color: #555; vertical-align: top;'>Fecha y Hora:</td>
                                                                        <td style='padding: 8px 0; color: #333; vertical-align: top;'>{fechaPago}</td>
                                                                    </tr>
                                                                    <tr>
                                                                        <td style='padding: 8px 0; font-weight: bold; color: #555; vertical-align: top;'>Monto Total:</td>
                                                                        <td style='padding: 8px 0; color: #e91e63; font-size: 22px; font-weight: bold; vertical-align: top;'>{monto} {moneda}</td>
                                                                    </tr>
                                                                    <tr>
                                                                        <td style='padding: 8px 0; font-weight: bold; color: #555; vertical-align: top;'>Plataforma:</td>
                                                                        <td style='padding: 8px 0; color: #333; vertical-align: top;'>Cobalt Payment Gateway</td>
                                                                    </tr>
                                                                    <tr>
                                                                        <td style='padding: 8px 0; font-weight: bold; color: #555; vertical-align: top;'>ID Transacción:</td>
                                                                        <td style='padding: 8px 0; color: #333; font-family: monospace; font-size: 14px; vertical-align: top;'>{transactionId}</td>
                                                                    </tr>
                                                                    {(string.IsNullOrEmpty(authNumber) ? "" : $@"
                                                                    <tr>
                                                                        <td style='padding: 8px 0; font-weight: bold; color: #555; vertical-align: top;'>Autorización:</td>
                                                                        <td style='padding: 8px 0; color: #333; font-family: monospace; font-size: 14px; vertical-align: top;'>{authNumber}</td>
                                                                    </tr>")}
                                                                    <tr>
                                                                        <td style='padding: 8px 0; font-weight: bold; color: #555; vertical-align: top;'>Estado:</td>
                                                                        <td style='padding: 8px 0; color: #4caf50; font-weight: bold; font-size: 16px; vertical-align: top;'>✅ PROCESADO EXITOSAMENTE</td>
                                                                    </tr>
                                                                </table>
                                                            </td>
                                                        </tr>
                                                    </table>
                                                    
                                                    <!-- Detalle de Facturas -->
                                                    <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #fff8e1; border-left: 4px solid #ff9800; border-radius: 0 8px 8px 0; margin: 20px 0;'>
                                                        <tr>
                                                            <td style='padding: 20px;'>
                                                                <h3 style='margin-top: 0; color: #ef6c00; font-size: 18px; margin-bottom: 15px;'>📋 Detalle de Facturas</h3>
                                                                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='border-collapse: collapse;'>
                                                                    <thead>
                                                                        <tr style='background-color: #ff9800;'>
                                                                            <th style='padding: 10px; color: white; text-align: left; border-radius: 4px 0 0 4px;'>Número de Factura</th>
                                                                            <th style='padding: 10px; color: white; text-align: right; border-radius: 0 4px 4px 0;'>Monto</th>
                                                                        </tr>
                                                                    </thead>
                                                                    <tbody>
                                                                        <tr style='background-color: white;'>
                                                                            <td style='padding: 12px; border-bottom: 1px solid #e0e0e0; color: #333;'>{descripcion.Replace("Pago de factura ", "")}</td>
                                                                            <td style='padding: 12px; border-bottom: 1px solid #e0e0e0; color: #ff9800; font-weight: bold; text-align: right;'>{moneda} {monto}</td>
                                                                        </tr>
                                                                    </tbody>
                                                                </table>
                                                            </td>
                                                        </tr>
                                                    </table>
                                                    
                                                    <!-- Nota de acción -->
                                                    <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #e8f5e8; border: 1px solid #c3e6cb; border-radius: 8px; margin: 20px 0;'>
                                                        <tr>
                                                            <td style='padding: 15px;'>
                                                                <p style='margin: 0; color: #155724; font-size: 14px; line-height: 1.4;'>
                                                                    <strong>✅ Acción requerida:</strong> Verificar que el producto/servicio haya sido entregado correctamente al cliente.
                                                                </p>
                                                            </td>
                                                        </tr>
                                                    </table>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                
                                <!-- Footer -->
                                <tr>
                                    <td style='text-align: center; padding: 30px 20px; background-color: #f8f9fa; border-radius: 0 0 8px 8px;'>
                                        <p style='color: #6c757d; font-size: 12px; margin: 0; line-height: 1.4;'>
                                            © {DateTime.Now.Year} Celero Network - Sistema de Notificaciones Administrativas
                        </p>
                    </div>
                </div>
            </body>
            </html>";
        }

        /// <summary>
        /// Endpoint para descargar PDF de facturas desde WebPOS FEPA
        /// Soporta GET con parámetro en la URL
        /// </summary>
        /// <param name="empresa">Código de empresa</param>
        /// <param name="noFactura">Número de factura a descargar</param>
        /// <returns>Archivo PDF para descarga</returns>
        [HttpGet("download-pdf/{empresa}/{noFactura}")]
        public async Task<IActionResult> DownloadPdf(string empresa, string noFactura)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(empresa))
                {
                    return BadRequest(new { message = "El código de empresa es requerido" });
                }
                
                if (string.IsNullOrWhiteSpace(noFactura))
                {
                    return BadRequest(new { message = "El número de factura es requerido" });
                }

                var cufe = await GetCufeFromFactura(empresa, noFactura);

                if (string.IsNullOrWhiteSpace(cufe))
                {
                    return NotFound(new
                    {
                        message = "No se encontró CUFE para la factura especificada",
                        ivSerNr = noFactura
                    });
                }


                _logger.LogInformation("Iniciando descarga de PDF para factura: {NoFactura}", cufe);

                // Usar el método con detalles para obtener el nombre original
                string apiUrl = _pdfDownloaderService.BuildWebPosApiUrl(cufe);
                var result = await _pdfDownloaderService.DownloadPdfWithDetailsAsync(apiUrl);

                if (!result.Success || result.PdfData == null || result.PdfData.Length == 0)
                {
                    _logger.LogWarning("No se pudo obtener datos del PDF para factura: {NoFactura}. Error: {Error}",
                        noFactura, result.ErrorMessage);
                    return NotFound(new { message = $"No se encontró el PDF para la factura: {noFactura}" });
                }

                _logger.LogInformation("PDF descargado exitosamente para factura: {NoFactura}, tamaño: {Size} bytes",
                    noFactura, result.PdfData.Length);

                // Usar el nombre de archivo de WebPOS con lógica robusta
                var fileName = _pdfDownloaderService.GetPdfFileName(result, $"Factura_{noFactura}_{DateTime.Now:yyyyMMdd}.pdf");

                // Retornar el archivo PDF
                return File(result.PdfData, "application/pdf", fileName);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Error de conexión al descargar PDF para factura: {NoFactura}", noFactura);
                return StatusCode(502, new { message = "Error de conexión con el servicio externo", details = httpEx.Message });
            }
            catch (ArgumentException argEx)
            {
                _logger.LogError(argEx, "Parámetro inválido para factura: {NoFactura}", noFactura);
                return BadRequest(new { message = "Número de factura inválido", details = argEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al descargar PDF para factura: {NoFactura}", noFactura);
                return StatusCode(500, new { message = "Error interno del servidor", details = ex.Message });
            }
        }

        /// <summary>
        /// Endpoint para descargar PDF de facturas desde WebPOS FEPA
        /// Soporta POST con cuerpo JSON
        /// </summary>
        /// <param name="request">Objeto con el número de factura</param>
        /// <returns>Archivo PDF para descarga</returns>
        [HttpPost("download-pdf")]
        public async Task<IActionResult> DownloadPdfPost([FromBody] PdfDownloadRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.NoFactura))
                {
                    return BadRequest(new { message = "El número de factura es requerido" });
                }

                _logger.LogInformation("Iniciando descarga de PDF (POST) para factura: {NoFactura}", request.NoFactura);

                // Usar el método con detalles para obtener el nombre original
                string apiUrl = _pdfDownloaderService.BuildWebPosApiUrl(request.NoFactura);
                var result = await _pdfDownloaderService.DownloadPdfWithDetailsAsync(apiUrl);

                if (!result.Success || result.PdfData == null || result.PdfData.Length == 0)
                {
                    _logger.LogWarning("No se pudo obtener datos del PDF para factura: {NoFactura}. Error: {Error}",
                        request.NoFactura, result.ErrorMessage);
                    return NotFound(new { message = $"No se encontró el PDF para la factura: {request.NoFactura}" });
                }

                _logger.LogInformation("PDF descargado exitosamente (POST) para factura: {NoFactura}, tamaño: {Size} bytes",
                    request.NoFactura, result.PdfData.Length);

                // Usar el nombre de archivo de WebPOS con lógica robusta
                var fileName = _pdfDownloaderService.GetPdfFileName(result, $"Factura_{request.NoFactura}_{DateTime.Now:yyyyMMdd}.pdf");

                // Retornar el archivo PDF
                return File(result.PdfData, "application/pdf", fileName);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Error de conexión al descargar PDF (POST) para factura: {NoFactura}", request?.NoFactura);
                return StatusCode(502, new { message = "Error de conexión con el servicio externo", details = httpEx.Message });
            }
            catch (ArgumentException argEx)
            {
                _logger.LogError(argEx, "Parámetro inválido para factura: {NoFactura}", request?.NoFactura);
                return BadRequest(new { message = "Número de factura inválido", details = argEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al descargar PDF (POST) para factura: {NoFactura}", request?.NoFactura);
                return StatusCode(500, new { message = "Error interno del servidor", details = ex.Message });
            }
        }

        /// <summary>
        /// Determina el tipo de documento basado en PDType
        /// </summary>
        /// <param name="pdType">Valor de PDType de la factura</param>
        /// <returns>Tipo de documento: "Nota de Débito", "Nota de Crédito" o "Factura"</returns>
        private string GetTipoDocumento(string pdType)
        {
            if (pdType == "5")
                return "Nota de Débito";
            else if (pdType == "3" || pdType == "9")
                return "Nota de Crédito";
            else
                return "Factura";
        }

        /// <summary>
        /// Obtiene el CUFE de una factura basado en el número de serie (IVSerNr)
        /// </summary>
        /// <param name="ivSerNr">Número de serie de la factura</param>
        /// <returns>CUFE de la factura si existe, null si no se encuentra o está vacío</returns>
        private async Task<string?> GetCufeFromFactura(string empresa, string ivSerNr)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ivSerNr))
                {
                    _logger.LogWarning("IVSerNr vacío o nulo");
                    return null;
                }

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(_hansaSettings.TimeoutSeconds);

                // Configurar autenticación básica usando la configuración
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_hansaSettings.Usuario}:{_hansaSettings.Clave}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                // Configurar headers
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Construir URL con el parámetro IVSerNr y empresa del endpoint
                string apiUrl = $"{_hansaSettings.GetFullBaseUrl()}/api/{empresa}/ApiWPReturnVc?sort=IVSerNr&range={ivSerNr}&fields=IVSerNr,cufe";

                _logger.LogInformation("Consultando CUFE para IVSerNr: {IVSerNr} desde: {Url}", ivSerNr, apiUrl);

                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error al consultar CUFE para IVSerNr {IVSerNr}: {StatusCode}", ivSerNr, response.StatusCode);
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                ApiWPReturnResponse? apiResponse = null;
                try
                {
                    apiResponse = JsonSerializer.Deserialize<ApiWPReturnResponse>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo deserializar la respuesta de CUFE para IVSerNr {IVSerNr}. Contenido crudo: {RawContent}", ivSerNr, jsonContent);
                    return null;
                }

                if (apiResponse?.data?.ApiWPReturnVc == null)
                {
                    _logger.LogWarning("No se encontraron datos para IVSerNr: {IVSerNr}", ivSerNr);
                    return null;
                }

                // Buscar un CUFE que no esté vacío
                var itemConCufe = apiResponse.data.ApiWPReturnVc
                    .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.cufe));

                if (itemConCufe != null)
                {
                    _logger.LogInformation("CUFE encontrado para IVSerNr {IVSerNr}: {Cufe}", ivSerNr, itemConCufe.cufe);
                    return itemConCufe.cufe;
                }

                _logger.LogWarning("No se encontró CUFE válido (no vacío) para IVSerNr: {IVSerNr}", ivSerNr);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener CUFE para IVSerNr: {IVSerNr}", ivSerNr);
                return null;
            }
        }

        /// <summary>
        /// Endpoint público para obtener el CUFE de una factura
        /// </summary>
        /// <param name="ivSerNr">Número de serie de la factura</param>
        /// <returns>CUFE de la factura o mensaje de error</returns>
        [HttpGet("cufe/{empresa}/{ivSerNr}")]
        public async Task<IActionResult> GetCufe(string empresa, string ivSerNr)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ivSerNr))
                {
                    return BadRequest(new { message = "El número de serie de factura (IVSerNr) es requerido" });
                }

                _logger.LogInformation("Solicitud de CUFE para IVSerNr: {IVSerNr} y Empresa: {Empresa}", ivSerNr, empresa);

                var cufe = await GetCufeFromFactura(empresa, ivSerNr);

                if (string.IsNullOrWhiteSpace(cufe))
                {
                    return NotFound(new
                    {
                        message = "No se encontró CUFE para la factura especificada",
                        ivSerNr = ivSerNr
                    });
                }

                return Ok(new
                {
                    ivSerNr = ivSerNr,
                    cufe = cufe,
                    message = "CUFE encontrado exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al obtener CUFE para IVSerNr: {IVSerNr}", ivSerNr);
                return StatusCode(500, new
                {
                    message = "Error interno del servidor",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Endpoint para descargar PDF usando el número de serie interno (IVSerNr)
        /// Primero obtiene el CUFE y luego descarga el PDF
        /// </summary>
        /// <param name="empresa">Código de empresa</param>
        /// <param name="ivSerNr">Número de serie interno de la factura</param>
        /// <returns>Archivo PDF para descarga</returns>
        [HttpGet("download-pdf-by-serie/{empresa}/{ivSerNr}")]
        public async Task<IActionResult> DownloadPdfBySerie(string empresa, string ivSerNr)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(empresa))
                {
                    return BadRequest(new { message = "El código de empresa es requerido" });
                }
                
                if (string.IsNullOrWhiteSpace(ivSerNr))
                {
                    return BadRequest(new { message = "El número de serie de factura (IVSerNr) es requerido" });
                }

                _logger.LogInformation("Iniciando descarga de PDF por serie: {IVSerNr} para empresa: {Empresa}", ivSerNr, empresa);

                // Primero obtener el CUFE
                var cufe = await GetCufeFromFactura(empresa, ivSerNr);

                if (string.IsNullOrWhiteSpace(cufe))
                {
                    _logger.LogWarning("No se pudo obtener CUFE para IVSerNr: {IVSerNr}", ivSerNr);
                    return NotFound(new { message = $"No se encontró CUFE para la factura con serie: {ivSerNr}" });
                }

                _logger.LogInformation("CUFE obtenido para serie {IVSerNr}: {Cufe}", ivSerNr, cufe);

                // Ahora descargar el PDF usando el CUFE
                string apiUrl = _pdfDownloaderService.BuildWebPosApiUrl(cufe);
                var result = await _pdfDownloaderService.DownloadPdfWithDetailsAsync(apiUrl);

                if (!result.Success || result.PdfData == null || result.PdfData.Length == 0)
                {
                    _logger.LogWarning("No se pudo descargar PDF para CUFE: {Cufe}. Error: {Error}",
                        cufe, result.ErrorMessage);
                    return NotFound(new { message = $"No se encontró el PDF para la factura con CUFE: {cufe}" });
                }

                _logger.LogInformation("PDF descargado exitosamente para serie {IVSerNr}, CUFE: {Cufe}, tamaño: {Size} bytes",
                    ivSerNr, cufe, result.PdfData.Length);

                // Usar el nombre de archivo de WebPOS con lógica robusta
                var fileName = _pdfDownloaderService.GetPdfFileName(result, $"Factura_Serie_{ivSerNr}_{DateTime.Now:yyyyMMdd}.pdf");

                // Retornar el archivo PDF
                return File(result.PdfData, "application/pdf", fileName);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Error de conexión al descargar PDF por serie: {IVSerNr}", ivSerNr);
                return StatusCode(502, new { message = "Error de conexión con el servicio externo", details = httpEx.Message });
            }
            catch (ArgumentException argEx)
            {
                _logger.LogError(argEx, "Parámetro inválido para serie: {IVSerNr}", ivSerNr);
                return BadRequest(new { message = "Número de serie inválido", details = argEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al descargar PDF por serie: {IVSerNr}", ivSerNr);
                return StatusCode(500, new { message = "Error interno del servidor", details = ex.Message });
            }
        }

        /// <summary>
        /// Endpoint temporal para diagnóstico: expone la respuesta cruda de la API externa
        /// </summary>
        /// <param name="ivSerNr">Número de serie de la factura</param>
        /// <returns>Respuesta cruda de la API externa para diagnóstico</returns>
        [HttpGet("debug-cufe/{ivSerNr}")]
        public async Task<IActionResult> DebugCufe(string ivSerNr)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ivSerNr))
                {
                    return BadRequest(new { message = "El número de serie de factura (IVSerNr) es requerido" });
                }

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(_hansaSettings.TimeoutSeconds);

                // Configurar autenticación básica usando la configuración
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_hansaSettings.Usuario}:{_hansaSettings.Clave}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Construir URL usando la configuración
                string apiUrl = $"{_hansaSettings.GetFullBaseUrl()}/api/{_hansaSettings.CompanyCode}/ApiWPReturnVc?sort=IVSerNr&range={ivSerNr}&fields=IVSerNr,cufe";


                var response = await httpClient.GetAsync(apiUrl);
                var rawContent = await response.Content.ReadAsStringAsync();
                var contentType = response.Content.Headers.ContentType?.ToString();

                var debugInfo = new
                {
                    ivSerNr = ivSerNr,
                    apiUrl = apiUrl,
                    statusCode = (int)response.StatusCode,
                    statusCodeName = response.StatusCode.ToString(),
                    isSuccess = response.IsSuccessStatusCode,
                    contentType = contentType,
                    contentLength = rawContent?.Length ?? 0,
                    rawContent = rawContent,
                    headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                    timestamp = DateTime.UtcNow
                };

                if (!response.IsSuccessStatusCode)
                {
                    return Ok(new
                    {
                        message = "La API externa devolvió un error",
                        debug = debugInfo
                    });
                }

                // Intentar deserializar para ver si es JSON válido
                ApiWPReturnResponse? apiResponse = null;
                string? deserializationError = null;
                try
                {
                    if (!string.IsNullOrEmpty(rawContent))
                    {
                        apiResponse = JsonSerializer.Deserialize<ApiWPReturnResponse>(rawContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    deserializationError = ex.Message;
                }

                return Ok(new
                {
                    message = "Información de diagnóstico para la consulta CUFE",
                    debug = debugInfo,
                    deserialization = new
                    {
                        success = apiResponse != null,
                        error = deserializationError,
                        hasData = apiResponse?.data != null,
                        hasApiWPReturnVc = apiResponse?.data?.ApiWPReturnVc != null,
                        itemCount = apiResponse?.data?.ApiWPReturnVc?.Count ?? 0,
                        items = apiResponse?.data?.ApiWPReturnVc?.Select(item => new
                        {
                            ivSerNr = item.IVSerNr,
                            cufe = item.cufe,
                            cufeIsEmpty = string.IsNullOrWhiteSpace(item.cufe)
                        })?.ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en debug CUFE para IVSerNr: {IVSerNr}", ivSerNr);
                return StatusCode(500, new
                {
                    message = "Error en el diagnóstico",
                    details = ex.Message,
                    ivSerNr = ivSerNr
                });
            }
        }

        /// <summary>
        /// Endpoint para diagnóstico de facturas abiertas
        /// Expone la URL consultada y la respuesta cruda de la API externa
        /// </summary>
        /// <param name="compCode">Código de la compañía</param>
        /// <param name="range">Rango de facturas (opcional)</param>
        /// <returns>Respuesta de diagnóstico</returns>
        [HttpGet("debug-facturas-abiertas/{compCode}")]
        public async Task<IActionResult> DebugFacturasAbiertas(string compCode, [FromQuery] string range)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(_hansaSettings.TimeoutSeconds);

                // Configurar autenticación básica
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_hansaSettings.Usuario}:{_hansaSettings.Clave}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Construir URL
                string apiUrl = $"{_hansaSettings.GetFullBaseUrl()}/api/{compCode}/ARVc?sort=CustCode&range={range}&fields=InvoiceNr,BookRVal";


                var response = await httpClient.GetAsync(apiUrl);
                var rawContent = await response.Content.ReadAsStringAsync();
                var contentType = response.Content.Headers.ContentType?.ToString();

                var debugInfo = new
                {
                    compCode = compCode,
                    range = range,
                    apiUrl = apiUrl,
                    statusCode = (int)response.StatusCode,
                    statusCodeName = response.StatusCode.ToString(),
                    isSuccess = response.IsSuccessStatusCode,
                    contentType = contentType,
                    contentLength = rawContent?.Length ?? 0,
                    rawContent = rawContent,
                    timestamp = DateTime.UtcNow
                };

                if (!response.IsSuccessStatusCode)
                {
                    return Ok(new
                    {
                        message = "La API externa devolvió un error",
                        debug = debugInfo
                    });
                }

                // Intentar deserializar para ver si es JSON válido
                Models.FacturaAbiertaResponse? facturasAbiertas = null;
                string? deserializationError = null;
                try
                {
                    if (!string.IsNullOrEmpty(rawContent))
                    {
                        facturasAbiertas = System.Text.Json.JsonSerializer.Deserialize<Models.FacturaAbiertaResponse>(rawContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    deserializationError = ex.Message;
                }

                return Ok(new
                {
                    message = "Información de diagnóstico para facturas abiertas",
                    debug = debugInfo,
                    deserialization = new
                    {
                        success = facturasAbiertas != null,
                        error = deserializationError,
                        hasData = facturasAbiertas?.data != null,
                        hasARVc = facturasAbiertas?.data?.ARVc != null,
                        itemCount = facturasAbiertas?.data?.ARVc?.Count ?? 0,
                        items = facturasAbiertas?.data?.ARVc?.Take(5)?.Select(item => new
                        {
                            invoiceNr = item.InvoiceNr,
                            bookRVal = item.BookRVal,
                            invoiceNrIsEmpty = string.IsNullOrWhiteSpace(item.InvoiceNr),
                            bookRValIsEmpty = string.IsNullOrWhiteSpace(item.BookRVal)
                        })?.ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en debug facturas abiertas para compCode: {CompCode}", compCode);
                return StatusCode(500, new
                {
                    message = "Error en el diagnóstico",
                    details = ex.Message,
                    compCode = compCode
                });
            }
        }

        [HttpGet("recibos-caja")]
        public async Task<IActionResult> GetRecibosCaja(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFin,
            [FromQuery] string custCode,
            [FromQuery] string? companyCode = null,
            [FromQuery] string? payMode = null)
        {
            if (string.IsNullOrEmpty(fechaInicio) || string.IsNullOrEmpty(fechaFin) || string.IsNullOrEmpty(custCode))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Parámetros requeridos: fechaInicio, fechaFin y custCode",
                    data = new List<object>(),
                    meta = new
                    {
                        total = 0,
                        filtros = new { fechaInicio = "", fechaFin = "", custCode = "", companyCode = "", payMode = "" },
                        empresasDisponibles = _hansaSettings.Companies.Where(c => c.ActiveStatus == "Activo").Select(c => new { c.CompCode, c.CompName, c.ShortName }),
                        timestamp = DateTime.UtcNow
                    }
                });
            }

            // Si no se especifica empresa, usar la por defecto
            string empresaAUsar = companyCode ?? _hansaSettings.CompanyCode;

            // Validar que la empresa existe y está activa
            var empresaValida = _hansaSettings.Companies.FirstOrDefault(c =>
                c.CompCode == empresaAUsar && c.ActiveStatus == "Activo");

            if (empresaValida == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Empresa '{empresaAUsar}' no encontrada o inactiva",
                    data = new List<object>(),
                    meta = new
                    {
                        total = 0,
                        error = "Empresa inválida",
                        empresasSolicitada = empresaAUsar,
                        empresasDisponibles = _hansaSettings.Companies.Where(c => c.ActiveStatus == "Activo").Select(c => new { c.CompCode, c.CompName, c.ShortName }),
                        filtros = new { fechaInicio, fechaFin, custCode, companyCode, payMode },
                        timestamp = DateTime.UtcNow
                    }
                });
            }

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(_hansaSettings.TimeoutSeconds);
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_hansaSettings.Usuario}:{_hansaSettings.Clave}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                string baseApiUrl = $"{_hansaSettings.GetFullBaseUrl()}/api/{empresaAUsar}";
                string range = $"{fechaInicio}:{fechaFin}";
                string url = $"{baseApiUrl}/IPVc?sort=TransDate&range={range}&fields=SerNr,TransDate,CurPayVal,InvoiceNr,RecVal,PayDate,CustCode,stp,Person,RefStr,Comment,PayMode&filter.OKFlag=1";
                
                // Agregar filtro por PayMode si se especifica
                if (!string.IsNullOrEmpty(payMode))
                {
                    url += $"&filter.PayMode={Uri.EscapeDataString(payMode)}";
                }

                _logger.LogInformation("Consultando recibos de caja para empresa {CompanyCode}: {Url}", empresaAUsar, url);

                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        success = false,
                        message = $"Error en API externa: {response.ReasonPhrase}",
                        data = new List<object>(),
                        meta = new
                        {
                            total = 0,
                            error = response.ReasonPhrase,
                            statusCode = (int)response.StatusCode,
                            empresa = new { empresaValida.CompCode, empresaValida.CompName, empresaValida.ShortName },
                            filtros = new { fechaInicio, fechaFin, custCode, companyCode = empresaAUsar },
                            timestamp = DateTime.UtcNow
                        }
                    });
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                var data = root.GetProperty("data");
                var ipvc = data.GetProperty("IPVc");
                var recibosOptimizados = new List<object>();
                decimal totalRecaudado = 0;

                foreach (var recibo in ipvc.EnumerateArray())
                {
                    var serNr = recibo.TryGetProperty("SerNr", out var serNrProp) ? serNrProp.GetString() : "";
                    var transDate = recibo.TryGetProperty("TransDate", out var transDateProp) ? transDateProp.GetString() : "";
                    var curPayVal = recibo.TryGetProperty("CurPayVal", out var curPayValProp) ? curPayValProp.GetString() : "0";
                    var person = recibo.TryGetProperty("Person", out var personProp) ? personProp.GetString() : "";
                    var refStr = recibo.TryGetProperty("RefStr", out var refStrProp) ? refStrProp.GetString() : "";
                    var comment = recibo.TryGetProperty("Comment", out var commentProp) ? commentProp.GetString() : "";
                    var payModeValue = recibo.TryGetProperty("PayMode", out var payModeProp) ? payModeProp.GetString() : "";

                    if (recibo.TryGetProperty("rows", out var rows))
                    {
                        var detallesFiltrados = new List<object>();
                        foreach (var row in rows.EnumerateArray())
                        {
                            if (row.TryGetProperty("CustCode", out var custCodeProp) && custCodeProp.GetString() == custCode)
                            {
                                var invoiceNr = row.TryGetProperty("InvoiceNr", out var invProp) ? invProp.GetString() : "";
                                var recVal = row.TryGetProperty("RecVal", out var recValProp) ? recValProp.GetString() : "0";
                                var payDate = row.TryGetProperty("PayDate", out var payDateProp) ? payDateProp.GetString() : "";
                                var stp = row.TryGetProperty("stp", out var stpProp) ? stpProp.GetString() : "";

                                detallesFiltrados.Add(new
                                {
                                    id = $"{serNr}-{invoiceNr}",
                                    invoiceNr = invoiceNr,
                                    recVal = recVal,
                                    recValNum = decimal.TryParse(recVal, out var rv) ? rv : 0,
                                    payDate = payDate,
                                    stp = stp,
                                    custCode = custCode
                                });

                                if (decimal.TryParse(recVal, out var recValDecimal))
                                {
                                    totalRecaudado += recValDecimal;
                                }
                            }
                        }

                        if (detallesFiltrados.Count > 0)
                        {
                            recibosOptimizados.Add(new
                            {
                                id = serNr,
                                serNr = serNr,
                                transDate = transDate,
                                transDateFormatted = DateTime.TryParse(transDate, out var td) ? td.ToString("dd/MM/yyyy") : transDate,
                                curPayVal = curPayVal,
                                curPayValNum = decimal.TryParse(curPayVal, out var cpv) ? cpv : 0,
                                person = person,
                                refStr = refStr,
                                comment = comment,
                                payMode = payModeValue,
                                custCode = custCode,
                                detalles = detallesFiltrados,
                                totalDetalles = detallesFiltrados.Count
                            });
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = $"Se encontraron {recibosOptimizados.Count} recibos de caja para {empresaValida.CompName}",
                    data = recibosOptimizados,
                    meta = new
                    {
                        total = recibosOptimizados.Count,
                        totalRecaudado = totalRecaudado,
                        totalRecaudadoFormatted = totalRecaudado.ToString("C"),
                        empresa = new { empresaValida.CompCode, empresaValida.CompName, empresaValida.ShortName },
                        filtros = new { fechaInicio, fechaFin, custCode, companyCode = empresaAUsar },
                        timestamp = DateTime.UtcNow,
                        apiUrl = url.Split('?')[0]
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consultando recibos de caja para cliente {CustCode} en empresa {CompanyCode}", custCode, empresaAUsar);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor",
                    data = new List<object>(),
                    meta = new
                    {
                        total = 0,
                        error = ex.Message,
                        empresa = empresaValida != null ? new { empresaValida.CompCode, empresaValida.CompName, empresaValida.ShortName } : null,
                        filtros = new { fechaInicio, fechaFin, custCode, companyCode = empresaAUsar },
                        timestamp = DateTime.UtcNow
                    }
                });
            }
        }

        [HttpGet("empresas-disponibles")]
        public IActionResult GetEmpresasDisponibles()
        {
            try
            {
                var empresasActivas = _hansaSettings.Companies
                    .Where(c => c.ActiveStatus == "Activo")
                    .Select(c => new {
                        c.CompCode,
                        c.CompName,
                        c.ShortName,
                        c.ActiveStatus,
                        isDefault = c.CompCode == _hansaSettings.CompanyCode
                    })
                    .OrderBy(c => c.CompCode)
                    .ToList();

                return Ok(new {
                    success = true,
                    message = $"Se encontraron {empresasActivas.Count} empresas activas",
                    data = empresasActivas,
                    meta = new {
                        total = empresasActivas.Count,
                        empresaDefault = _hansaSettings.CompanyCode,
                        timestamp = DateTime.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener empresas disponibles");
                return StatusCode(500, new {
                    success = false,
                    message = "Error interno del servidor",
                    data = new List<object>(),
                    meta = new {
                        total = 0,
                        error = ex.Message,
                        timestamp = DateTime.UtcNow
                    }
                });
            }
        }

        #region Endpoints para Pagos ACH

        /// <summary>
        /// Registra una foto de comprobante de pago ACH en SQLite
        /// </summary>
        /// <param name="request">Datos del pago ACH</param>
        /// <returns>Respuesta del registro</returns>
        [HttpPost("pago-ach")]
        public async Task<IActionResult> RegistrarPagoACH([FromForm] PagoACHRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new PagoACHResponse
                    {
                        Success = false,
                        Message = "Datos inválidos: " + string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))
                    });
                }

                // Validar archivo
                if (request.FotoComprobante == null || request.FotoComprobante.Length == 0)
                {
                    return BadRequest(new PagoACHResponse
                    {
                        Success = false,
                        Message = "La foto del comprobante es requerida"
                    });
                }

                // Validar tipos de archivo permitidos
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "application/pdf" };
                if (!allowedTypes.Contains(request.FotoComprobante.ContentType.ToLower()))
                {
                    return BadRequest(new PagoACHResponse
                    {
                        Success = false,
                        Message = "Tipo de archivo no permitido. Solo se permiten: JPG, PNG, GIF, PDF"
                    });
                }

                // Validar tamaño (máximo 5MB)
                const int maxSize = 5 * 1024 * 1024; // 5MB
                if (request.FotoComprobante.Length > maxSize)
                {
                    return BadRequest(new PagoACHResponse
                    {
                        Success = false,
                        Message = "El archivo es demasiado grande. Tamaño máximo: 5MB"
                    });
                }

                // Convertir archivo a bytes
                byte[] fotoBytes;
                using (var memoryStream = new MemoryStream())
                {
                    await request.FotoComprobante.CopyToAsync(memoryStream);
                    fotoBytes = memoryStream.ToArray();
                }

                // Crear registro
                var pagoACH = new PagoACHFoto
                {
                    ClienteCode = request.ClienteCode,
                    NumeroFactura = request.NumeroFactura,
                    EmpresaCode = request.EmpresaCode,
                    NumeroTransaccion = request.NumeroTransaccion,
                    FotoComprobante = fotoBytes,
                    NombreArchivo = request.FotoComprobante.FileName,
                    TipoArchivo = request.FotoComprobante.ContentType,
                    TamanoArchivo = request.FotoComprobante.Length,
                    MontoTransaccion = request.MontoTransaccion,
                    FechaTransaccion = request.FechaTransaccion,
                    Observaciones = request.Observaciones,
                    UsuarioRegistro = request.UsuarioRegistro,
                    Estado = "PENDIENTE",
                    FechaRegistro = DateTime.Now
                };

                // Guardar en base de datos
                _dbContext.PagoACHFotos.Add(pagoACH);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Pago ACH registrado exitosamente. ID: {PagoId}, Cliente: {ClienteCode}, Factura: {NumeroFactura}", 
                    pagoACH.Id, request.ClienteCode, request.NumeroFactura);

                return Ok(new PagoACHResponse
                {
                    Success = true,
                    Message = "Pago ACH registrado exitosamente",
                    PagoId = pagoACH.Id,
                    FechaRegistro = pagoACH.FechaRegistro,
                    Estado = pagoACH.Estado
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar pago ACH para cliente {ClienteCode}", request?.ClienteCode);
                return StatusCode(500, new PagoACHResponse
                {
                    Success = false,
                    Message = "Error interno del servidor"
                });
            }
        }

        /// <summary>
        /// Consulta los pagos ACH registrados
        /// </summary>
        /// <param name="clienteCode">Código del cliente (opcional)</param>
        /// <param name="empresaCode">Código de empresa (opcional)</param>
        /// <param name="estado">Estado del pago (opcional)</param>
        /// <param name="fechaDesde">Fecha desde (opcional)</param>
        /// <param name="fechaHasta">Fecha hasta (opcional)</param>
        /// <returns>Lista de pagos ACH</returns>
        [HttpGet("pago-ach")]
        public async Task<IActionResult> ConsultarPagosACH(
            [FromQuery] string? clienteCode = null,
            [FromQuery] string? empresaCode = null,
            [FromQuery] string? estado = null,
            [FromQuery] DateTime? fechaDesde = null,
            [FromQuery] DateTime? fechaHasta = null)
        {
            try
            {
                var query = _dbContext.PagoACHFotos.AsQueryable();

                // Aplicar filtros
                if (!string.IsNullOrEmpty(clienteCode))
                    query = query.Where(p => p.ClienteCode == clienteCode);

                if (!string.IsNullOrEmpty(empresaCode))
                    query = query.Where(p => p.EmpresaCode == empresaCode);

                if (!string.IsNullOrEmpty(estado))
                    query = query.Where(p => p.Estado == estado);

                if (fechaDesde.HasValue)
                    query = query.Where(p => p.FechaRegistro >= fechaDesde.Value);

                if (fechaHasta.HasValue)
                    query = query.Where(p => p.FechaRegistro <= fechaHasta.Value);

                // Ordenar por fecha de registro descendente
                query = query.OrderByDescending(p => p.FechaRegistro);

                var pagos = await query
                    .Select(p => new PagoACHConsultaResponse
                    {
                        Id = p.Id,
                        ClienteCode = p.ClienteCode,
                        NumeroFactura = p.NumeroFactura,
                        EmpresaCode = p.EmpresaCode,
                        NumeroTransaccion = p.NumeroTransaccion,
                        NombreArchivo = p.NombreArchivo,
                        TipoArchivo = p.TipoArchivo,
                        TamanoArchivo = p.TamanoArchivo,
                        MontoTransaccion = p.MontoTransaccion,
                        FechaTransaccion = p.FechaTransaccion,
                        FechaRegistro = p.FechaRegistro,
                        Observaciones = p.Observaciones,
                        Estado = p.Estado,
                        UsuarioRegistro = p.UsuarioRegistro,
                        FechaProcesamiento = p.FechaProcesamiento,
                        MotivoRechazo = p.MotivoRechazo
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = pagos,
                    total = pagos.Count,
                    filtros = new
                    {
                        clienteCode,
                        empresaCode,
                        estado,
                        fechaDesde,
                        fechaHasta
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar pagos ACH");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor"
                });
            }
        }

        /// <summary>
        /// Obtiene la foto de un pago ACH específico
        /// </summary>
        /// <param name="pagoId">ID del pago ACH</param>
        /// <returns>Archivo de imagen</returns>
        [HttpGet("pago-ach/{pagoId}/foto")]
        public async Task<IActionResult> ObtenerFotoPagoACH(int pagoId)
        {
            try
            {
                var pago = await _dbContext.PagoACHFotos.FindAsync(pagoId);
                
                if (pago == null)
                {
                    return NotFound(new { message = "Pago ACH no encontrado" });
                }

                return File(pago.FotoComprobante, pago.TipoArchivo, pago.NombreArchivo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener foto del pago ACH {PagoId}", pagoId);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Actualiza el estado de un pago ACH
        /// </summary>
        /// <param name="pagoId">ID del pago ACH</param>
        /// <param name="nuevoEstado">Nuevo estado (PENDIENTE, PROCESADO, RECHAZADO)</param>
        /// <param name="motivoRechazo">Motivo del rechazo (opcional)</param>
        /// <returns>Respuesta de la actualización</returns>
        [HttpPut("pago-ach/{pagoId}/estado")]
        public async Task<IActionResult> ActualizarEstadoPagoACH(
            int pagoId,
            [FromQuery] string nuevoEstado,
            [FromQuery] string? motivoRechazo = null)
        {
            try
            {
                var estadosPermitidos = new[] { "PENDIENTE", "PROCESADO", "RECHAZADO" };
                if (!estadosPermitidos.Contains(nuevoEstado))
                {
                    return BadRequest(new { message = "Estado no válido. Estados permitidos: " + string.Join(", ", estadosPermitidos) });
                }

                var pago = await _dbContext.PagoACHFotos.FindAsync(pagoId);
                if (pago == null)
                {
                    return NotFound(new { message = "Pago ACH no encontrado" });
                }

                pago.Estado = nuevoEstado;
                pago.FechaProcesamiento = DateTime.Now;
                
                if (nuevoEstado == "RECHAZADO" && !string.IsNullOrEmpty(motivoRechazo))
                {
                    pago.MotivoRechazo = motivoRechazo;
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Estado del pago ACH {PagoId} actualizado a {NuevoEstado}", pagoId, nuevoEstado);

                return Ok(new
                {
                    success = true,
                    message = "Estado actualizado exitosamente",
                    pagoId = pagoId,
                    nuevoEstado = nuevoEstado,
                    fechaProcesamiento = pago.FechaProcesamiento
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar estado del pago ACH {PagoId}", pagoId);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        #endregion

        /// <summary>
        /// Obtiene las formas de pago disponibles para una empresa específica
        /// </summary>
        /// <param name="empresaCode">Código de la empresa</param>
        /// <returns>Formas de pago habilitadas para la empresa</returns>
        [HttpGet("payment-methods/{empresaCode}")]
        public IActionResult GetPaymentMethodsByCompany(string empresaCode)
        {
            try
            {
                // Buscar la empresa en la configuración
                var empresa = _hansaSettings.Companies.FirstOrDefault(c => c.CompCode == empresaCode);
                
                if (empresa == null)
                {
                    return NotFound(new {
                        success = false,
                        message = $"Empresa con código '{empresaCode}' no encontrada",
                        data = new { }
                    });
                }

                // Si la empresa no está activa, devolver todas las formas de pago deshabilitadas
                if (empresa.ActiveStatus != "Activo")
                {
                    return Ok(new {
                        success = true,
                        message = $"Empresa '{empresa.CompName}' no está activa",
                        data = new {
                            companyCode = empresa.CompCode,
                            companyName = empresa.CompName,
                            isActive = false,
                            paymentMethods = new {
                                creditCard = new { enabled = false, displayName = "Tarjeta de Crédito" },
                                yappy = new { enabled = false, displayName = "Yappy" },
                                ach = new { enabled = false, displayName = "ACH" }
                            }
                        }
                    });
                }

                // Devolver las formas de pago configuradas
                return Ok(new {
                    success = true,
                    message = "Formas de pago obtenidas exitosamente",
                    data = new {
                        companyCode = empresa.CompCode,
                        companyName = empresa.CompName,
                        shortName = empresa.ShortName,
                        isActive = true,
                        paymentMethods = new {
                            creditCard = new { 
                                enabled = empresa.PaymentMethods?.CreditCard?.Enabled ?? false,
                                displayName = empresa.PaymentMethods?.CreditCard?.DisplayName ?? "Tarjeta de Crédito"
                            },
                            yappy = new { 
                                enabled = empresa.PaymentMethods?.Yappy?.Enabled ?? false,
                                displayName = empresa.PaymentMethods?.Yappy?.DisplayName ?? "Yappy"
                            },
                            ach = new { 
                                enabled = empresa.PaymentMethods?.ACH?.Enabled ?? false,
                                displayName = empresa.PaymentMethods?.ACH?.DisplayName ?? "ACH"
                            },
                            paypal = new { 
                                enabled = empresa.PaymentMethods?.PayPal?.Enabled ?? false,
                                displayName = empresa.PaymentMethods?.PayPal?.DisplayName ?? "PayPal"
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener formas de pago para empresa {EmpresaCode}", empresaCode);
                return StatusCode(500, new {
                    success = false,
                    message = "Error interno del servidor",
                    data = new { }
                });
            }
        }

        /// <summary>
        /// Obtiene todas las empresas con sus formas de pago configuradas
        /// </summary>
        /// <returns>Lista de empresas con sus formas de pago</returns>
        [HttpGet("payment-methods")]
        public IActionResult GetAllPaymentMethods()
        {
            try
            {
                var empresasConPagos = _hansaSettings.Companies
                    .Where(c => c.ActiveStatus == "Activo")
                    .Select(c => new {
                        companyCode = c.CompCode,
                        companyName = c.CompName,
                        shortName = c.ShortName,
                        paymentMethods = new {
                            creditCard = new { 
                                enabled = c.PaymentMethods?.CreditCard?.Enabled ?? false,
                                displayName = c.PaymentMethods?.CreditCard?.DisplayName ?? "Tarjeta de Crédito"
                            },
                            yappy = new { 
                                enabled = c.PaymentMethods?.Yappy?.Enabled ?? false,
                                displayName = c.PaymentMethods?.Yappy?.DisplayName ?? "Yappy"
                            },
                            ach = new { 
                                enabled = c.PaymentMethods?.ACH?.Enabled ?? false,
                                displayName = c.PaymentMethods?.ACH?.DisplayName ?? "ACH"
                            },
                            paypal = new { 
                                enabled = c.PaymentMethods?.PayPal?.Enabled ?? false,
                                displayName = c.PaymentMethods?.PayPal?.DisplayName ?? "PayPal"
                            }
                        }
                    })
                    .OrderBy(c => c.companyCode)
                    .ToList();

                return Ok(new {
                    success = true,
                    message = $"Se encontraron {empresasConPagos.Count} empresas con configuración de pagos",
                    data = empresasConPagos,
                    meta = new {
                        total = empresasConPagos.Count,
                        timestamp = DateTime.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todas las formas de pago");
                return StatusCode(500, new {
                    success = false,
                    message = "Error interno del servidor",
                    data = new List<object>()
                });
            }
        }

        #region Endpoints para Pagos con PayPal

        /// <summary>
        /// Verifica la configuración de PayPal
        /// </summary>
        /// <returns>Estado de la configuración</returns>
        [HttpGet("paypal/config")]
        public IActionResult GetPayPalConfig()
        {
            try
            {
                return Ok(new {
                    success = true,
                    message = "Configuración de PayPal",
                    data = new {
                        baseUrl = _payPalSettings.BaseUrl,
                        mode = _payPalSettings.Mode,
                        clientIdConfigured = !string.IsNullOrEmpty(_payPalSettings.ClientId),
                        clientSecretConfigured = !string.IsNullOrEmpty(_payPalSettings.ClientSecret),
                        tokenUrl = _payPalSettings.TokenUrl,
                        ordersUrl = _payPalSettings.OrdersUrl
                    },
                    debug = new {
                        baseUrlValue = _payPalSettings.BaseUrl,
                        modeValue = _payPalSettings.Mode,
                        clientIdValue = _payPalSettings.ClientId?.Substring(0, Math.Min(10, _payPalSettings.ClientId.Length)) + "...",
                        tokenEndpoint = _payPalSettings.TokenEndpoint,
                        ordersEndpoint = _payPalSettings.OrdersEndpoint,
                        fullTokenUrl = _payPalSettings.TokenUrl,
                        fullOrdersUrl = _payPalSettings.OrdersUrl
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener configuración de PayPal");
                return StatusCode(500, new {
                    success = false,
                    message = "Error al obtener configuración de PayPal"
                });
            }
        }

        /// <summary>
        /// Prueba la conectividad con PayPal obteniendo un token
        /// </summary>
        /// <returns>Resultado de la prueba</returns>
        [HttpGet("paypal/test-connection")]
        public async Task<IActionResult> TestPayPalConnection()
        {
            try
            {
                var token = await GetPayPalAccessToken();
                
                return Ok(new {
                    success = !string.IsNullOrEmpty(token),
                    message = !string.IsNullOrEmpty(token) ? "Conexión exitosa con PayPal" : "Error al conectar con PayPal",
                    data = new {
                        tokenObtained = !string.IsNullOrEmpty(token),
                        tokenLength = token?.Length ?? 0
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al probar conexión con PayPal");
                return StatusCode(500, new {
                    success = false,
                    message = "Error al probar conexión con PayPal",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Crea una orden de pago con PayPal
        /// </summary>
        /// <param name="request">Datos para crear la orden de pago</param>
        /// <returns>URL de redirección a PayPal</returns>
        [HttpPost("paypal/create-order")]
        public async Task<IActionResult> CreatePayPalOrder([FromBody] PayPalCreateOrderRequest request)
        {
            try
            {
                _logger.LogInformation("Iniciando creación de orden PayPal para cliente {ClienteCode}, factura {NumeroFactura}", 
                    request.ClienteCode, request.NumeroFactura);

                // Validar datos de entrada
                if (string.IsNullOrEmpty(request.ClienteCode) || string.IsNullOrEmpty(request.NumeroFactura))
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "El código de cliente y número de factura son requeridos" 
                    });
                }

                if (request.Amount <= 0)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "El monto debe ser mayor a cero" 
                    });
                }

                // Obtener token de acceso de PayPal
                var token = await GetPayPalAccessToken();
                if (string.IsNullOrEmpty(token))
                {
                    return StatusCode(500, new { 
                        success = false, 
                        message = "Error al autenticar con PayPal" 
                    });
                }

                // Crear orden en PayPal
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var orderRequest = new PayPalApiCreateOrderRequest
                {
                    Intent = "CAPTURE",
                    PurchaseUnits = new List<PayPalApiPurchaseUnit>
                    {
                        new PayPalApiPurchaseUnit
                        {
                            ReferenceId = request.NumeroFactura,
                            Description = request.Description ?? $"Pago de factura {request.NumeroFactura}",
                            Amount = new PayPalApiAmount
                            {
                                CurrencyCode = request.Currency,
                                Value = request.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                            }
                        }
                    },
                    ApplicationContext = new PayPalApiApplicationContext
                    {
                        ReturnUrl = request.ReturnUrl,
                        CancelUrl = request.CancelUrl,
                        BrandName = "Celero Network",
                        LandingPage = "LOGIN",
                        UserAction = "PAY_NOW"
                    }
                };

                var jsonContent = System.Text.Json.JsonSerializer.Serialize(orderRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Usar configuración de producción
                var ordersUrl = string.IsNullOrEmpty(_payPalSettings.BaseUrl) 
                    ? "https://api-m.paypal.com/v2/checkout/orders"
                    : _payPalSettings.OrdersUrl;
                    
                _logger.LogInformation("Creando orden PayPal en: {OrdersUrl}", ordersUrl);

                var response = await httpClient.PostAsync(ordersUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error al crear orden PayPal: {StatusCode} - {Content}", 
                        response.StatusCode, responseContent);
                    return StatusCode(500, new { 
                        success = false, 
                        message = "Error al crear orden de pago en PayPal" 
                    });
                }

                var paypalResponse = System.Text.Json.JsonSerializer.Deserialize<PayPalCreateOrderResponse>(responseContent);
                
                // Buscar el enlace de aprobación
                var approvalLink = paypalResponse?.Links?.FirstOrDefault(l => l.Rel == "approve")?.Href;
                
                if (string.IsNullOrEmpty(approvalLink))
                {
                    _logger.LogError("No se encontró enlace de aprobación en respuesta de PayPal");
                    return StatusCode(500, new { 
                        success = false, 
                        message = "Error al obtener enlace de pago de PayPal" 
                    });
                }

                _logger.LogInformation("Orden PayPal creada exitosamente. ID: {OrderId}", paypalResponse?.Id);

                return Ok(new {
                    success = true,
                    message = "Orden de pago creada exitosamente",
                    data = new {
                        orderId = paypalResponse?.Id,
                        approvalUrl = approvalLink,
                        status = paypalResponse?.Status
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear orden PayPal para cliente {ClienteCode}", request?.ClienteCode);
                return StatusCode(500, new {
                    success = false,
                    message = "Error interno del servidor"
                });
            }
        }

        /// <summary>
        /// Captura un pago de PayPal después de la aprobación del usuario
        /// </summary>
        /// <param name="request">Datos para capturar el pago</param>
        /// <returns>Detalles del pago capturado</returns>
        [HttpPost("paypal/capture-payment")]
        public async Task<IActionResult> CapturePayPalPayment([FromBody] PayPalCaptureRequest request)
        {
            try
            {
                _logger.LogInformation("Iniciando captura de pago PayPal. OrderId: {OrderId}, Factura: {NumeroFactura}, Cliente: {ClienteCode}", 
                    request?.OrderId, request?.NumeroFactura, request?.ClienteCode);
                    
                    
                if (request?.InvoiceDetails != null && request.InvoiceDetails.Count > 0)
                {
                    for (int i = 0; i < request.InvoiceDetails.Count; i++)
                    {
                        var detail = request.InvoiceDetails[i];
                    }
                }

                // Validar datos de entrada
                if (request == null || string.IsNullOrEmpty(request.OrderId))
                {
                    _logger.LogWarning("Solicitud de captura PayPal inválida: request es null o OrderId vacío");
                    return BadRequest(new { 
                        success = false, 
                        message = "El ID de la orden es requerido",
                        errorCode = "INVALID_ORDER_ID"
                    });
                }

                // Obtener token de acceso de PayPal
                _logger.LogInformation("Obteniendo token de acceso PayPal...");
                var token = await GetPayPalAccessToken();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("No se pudo obtener token de PayPal. Verificar credenciales en configuración.");
                    return StatusCode(500, new { 
                        success = false, 
                        message = "Error al autenticar con PayPal. Por favor, intente nuevamente.",
                        errorCode = "PAYPAL_AUTH_FAILED",
                        details = "No se pudo obtener el token de acceso"
                    });
                }

                // Capturar pago en PayPal
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Construir URL de captura
                var baseUrl = !string.IsNullOrEmpty(_payPalSettings.BaseUrl) 
                    ? _payPalSettings.BaseUrl.TrimEnd('/')
                    : "https://api-m.paypal.com";
                var captureUrl = $"{baseUrl}/v2/checkout/orders/{request.OrderId}/capture";
                
                _logger.LogInformation("Capturando pago PayPal en: {CaptureUrl}, Mode: {Mode}", 
                    captureUrl, _payPalSettings.Mode);
                
                var response = await httpClient.PostAsync(captureUrl, new StringContent("", Encoding.UTF8, "application/json"));
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error al capturar pago PayPal: StatusCode={StatusCode}, OrderId={OrderId}, Content={Content}", 
                        response.StatusCode, request.OrderId, responseContent);
                    
                    // Intentar parsear el error de PayPal para dar más detalles
                    string errorMessage = "Error al capturar el pago en PayPal";
                    string errorCode = "PAYPAL_CAPTURE_FAILED";
                    
                    try
                    {
                        var errorObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseContent);
                        if (errorObj.TryGetProperty("name", out var name))
                        {
                            errorCode = name.GetString() ?? errorCode;
                            
                            if (errorObj.TryGetProperty("message", out var msg))
                            {
                                errorMessage = msg.GetString() ?? errorMessage;
                            }
                            
                            if (errorObj.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                            {
                                var firstDetail = details.EnumerateArray().FirstOrDefault();
                                if (firstDetail.TryGetProperty("description", out var desc))
                                {
                                    errorMessage = $"{errorMessage}: {desc.GetString()}";
                                }
                            }
                        }
                    }
                    catch (Exception parseEx)
                    {
                        _logger.LogWarning(parseEx, "No se pudo parsear el error de PayPal");
                    }
                    
                    return StatusCode((int)response.StatusCode, new { 
                        success = false, 
                        message = errorMessage,
                        errorCode = errorCode,
                        orderId = request.OrderId,
                        statusCode = (int)response.StatusCode
                    });
                }

                var captureResponse = System.Text.Json.JsonSerializer.Deserialize<PayPalCaptureResponse>(responseContent);

                // Verificar que el pago fue completado
                if (captureResponse?.Status != "COMPLETED")
                {
                    _logger.LogWarning("Pago PayPal no completado. Estado: {Status}", captureResponse?.Status);
                    return BadRequest(new {
                        success = false,
                        message = $"El pago no pudo ser completado. Estado: {captureResponse?.Status}"
                    });
                }

                // Extraer información del pago
                var capture = captureResponse.PurchaseUnits?.FirstOrDefault()?.Payments?.Captures?.FirstOrDefault();
                var montoOriginal = capture?.Amount?.Value ?? "0";
                
                // Normalizar el monto para asegurar formato correcto independientemente de la cultura
                // PayPal puede devolver "200,00" o "2.00" dependiendo de la configuración regional
                var monto = NormalizarMontoDecimal(montoOriginal);
                
                var moneda = capture?.Amount?.CurrencyCode ?? "USD";
                var transactionId = capture?.Id ?? "";
                var fechaPago = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                
                string? serNrRegistrado = null; // Variable para almacenar el número de recibo

                _logger.LogInformation("Pago PayPal capturado exitosamente. TransactionId: {TransactionId}, Monto: {Monto} {Moneda}", 
                    transactionId, monto, moneda);

                // Registrar el recibo en el ERP de Hansa después del pago exitoso
                try
                {
                    
                    // Obtener información del pagador desde la respuesta de PayPal
                    var nombreCliente = string.Empty;
                    var emailCliente = string.Empty;
                    
                    if (captureResponse?.Payer != null)
                    {
                        // Construir nombre completo desde PayPal
                        if (captureResponse.Payer.Name != null)
                        {
                            var givenName = captureResponse.Payer.Name.GivenName ?? "";
                            var surname = captureResponse.Payer.Name.Surname ?? "";
                            nombreCliente = $"{givenName} {surname}".Trim();
                        }
                        
                        // Usar email del cliente ERP (no de PayPal)
                        emailCliente = request.EmailCliente ?? "";
                        
                        _logger.LogInformation("Información del pagador desde PayPal: Nombre: {NombreCliente}, Email: {EmailCliente}", 
                            nombreCliente, emailCliente);
                    }
                    else
                    {
                        _logger.LogWarning("No se encontró información del pagador en la respuesta de PayPal");
                    }
                    
                    // Crear comentario con nombre y email desde PayPal
                    var comentario = "PayPal";
                    if (!string.IsNullOrEmpty(nombreCliente))
                        comentario += $" - {nombreCliente}";
                    if (!string.IsNullOrEmpty(emailCliente))
                        comentario += $" - {emailCliente}";
                    
                    // Crear la estructura del recibo usando el ID de la captura como referencia
                    var entry = new Entry
                    {
                        SerNr = transactionId, // Usar el capture ID como SerNr
                        TransDate = DateTime.Now.ToString("dd-MM-yyyy"),
                        PayMode = "PP", // PP para PayPal
                        Person = request.ClienteCode,
                        CUCode = request.ClienteCode,
                        RefStr = transactionId, // Usar el capture ID como referencia
                        Detalles = new List<Detalle>()
                    };
                    
                    // Procesar facturas usando InvoiceDetails (formato estándar)
                    if (request.InvoiceDetails != null && request.InvoiceDetails.Count > 0)
                    {
                        _logger.LogInformation($"Procesando {request.InvoiceDetails.Count} facturas con montos específicos");
                        
                        foreach (var invoiceDetail in request.InvoiceDetails)
                        {
                            entry.Detalles.Add(new Detalle
                            {
                                InvoiceNr = invoiceDetail.InvoiceNumber,
                                Sum = invoiceDetail.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                                Objects = comentario,
                                Stp = "1"
                            });
                            _logger.LogInformation($"Agregando factura {invoiceDetail.InvoiceNumber} con monto {invoiceDetail.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
                        }
                    }
                    // FALLBACK TEMPORAL: Soportar formato legacy durante la transición del frontend
                    else if (!string.IsNullOrEmpty(request.NumeroFactura))
                    {
                        _logger.LogWarning("USANDO FORMATO LEGACY - NumeroFactura. Frontend debe actualizarse para usar InvoiceDetails.");
                        
                        // PayPal ya envía el monto correcto en dólares, usar el valor normalizado
                        decimal montoDecimal = 0;
                        if (decimal.TryParse(monto, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out montoDecimal))
                        {
                        }
                        else
                        {
                            _logger.LogWarning("No se pudo parsear el monto PayPal normalizado: '{Monto}', usando 0", monto);
                        }
                        
                        entry.Detalles.Add(new Detalle
                        {
                            InvoiceNr = request.NumeroFactura,
                            Sum = montoDecimal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                            Objects = comentario,
                            Stp = "1"
                        });
                        _logger.LogInformation($"Agregando factura legacy {request.NumeroFactura} con monto {montoDecimal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
                    }
                    else
                    {
                        _logger.LogError("ATENCIÓN: InvoiceDetails o NumeroFactura es requerido.");
                        throw new ArgumentException("InvoiceDetails es requerido con el número de factura y monto específico para cada factura. O proporcione NumeroFactura como formato legacy temporal.");
                    }

                    var datos = new Dictionary<string, Entry> { { entry.SerNr, entry } };
                    
                    // Intentar registrar el recibo en Hansa
                    _logger.LogInformation("Datos para registro en Hansa: {@DatosRecibo}", datos);
                    serNrRegistrado = await AgregarRecibosDeCajasAsync(datos, _hansaSettings, _logger, _hansaReceiptService);
                    _logger.LogInformation("Resultado registro Hansa - SerNrRegistrado: {SerNr}", serNrRegistrado ?? "NULL");
                    
                    // Verificar que sea un SerNr válido o código 20878 (que también registra exitosamente)
                    if (!string.IsNullOrEmpty(serNrRegistrado))
                    {
                        _logger.LogInformation("Recibo PayPal registrado exitosamente en ERP. SerNr: {SerNr}", serNrRegistrado);
                        
                        // Enviar notificación usando el método reutilizable
                        await EnviarNotificacionReciboAsync(
                            serNrRegistrado, 
                            emailCliente, 
                            "PAYPAL",
                            decimal.TryParse(monto, out var montoDecimal) ? montoDecimal : 0m,
                            request.NumeroFactura,
                            request.ClienteCode,
                            nombreCliente // Pasar el nombre real obtenido de PayPal
                        );
                    }
                    else
                    {
                        _logger.LogWarning("No se pudo registrar el recibo PayPal en ERP, guardando offline para procesamiento posterior");
                        // Guardar en SQLite para procesamiento posterior
                        try
                        {
                            using (var scope = HttpContext.RequestServices.CreateScope())
                            {
                                var db = scope.ServiceProvider.GetRequiredService<ReciboCajaOfflineContext>();
                                var reciboOffline = new Models.ReciboCajaOffline
                                {
                                    SerNr = transactionId,
                                    TransDate = DateTime.Now,
                                    PayMode = "PP",
                                    Person = request.ClienteCode,
                                    CUCode = request.ClienteCode,
                                    RefStr = transactionId,
                                    DetallesJson = System.Text.Json.JsonSerializer.Serialize(entry.Detalles),
                                    Pendiente = true
                                };
                                db.RecibosOffline.Add(reciboOffline);
                                await db.SaveChangesAsync();
                                _logger.LogInformation("Recibo PayPal guardado offline para procesamiento posterior. TransactionId: {TransactionId}", transactionId);
                            }
                        }
                        catch (Exception exSqlite)
                        {
                            _logger.LogError(exSqlite, "Error al guardar recibo PayPal offline. TransactionId: {TransactionId}", transactionId);
                        }
                    }
                }
                catch (Exception exRecibo)
                {
                    _logger.LogError(exRecibo, "Error al registrar recibo PayPal. TransactionId: {TransactionId}", transactionId);
                    // No fallar la captura si falla el recibo - el pago ya fue exitoso
                }

                // Obtener información del cliente para el email
                var nombreClienteEmail = string.Empty;
                var emailClienteEnvio = string.Empty;
                
                using (var scope = HttpContext.RequestServices.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ReciboCajaOfflineContext>();
                    var clienteLocal = await db.ClientesLocales
                        .FirstOrDefaultAsync(c => c.Code == request.ClienteCode);
                    
                    if (clienteLocal != null)
                    {
                        nombreClienteEmail = clienteLocal.Name;
                        emailClienteEnvio = clienteLocal.eMail;
                    }
                }
                
                // NOTA: El email al cliente ahora se envía desde EnviarNotificacionReciboAsync con la plantilla corporativa correcta
                // Este bloque se conserva solo para el email a administradores
                if (!string.IsNullOrEmpty(emailClienteEnvio))
                {
                    try
                    {
                        _logger.LogInformation("Email al cliente PayPal ya enviado por EnviarNotificacionReciboAsync");

                        // Enviar email a administradores
                        var adminEmails = _configuration.GetSection("Email:AdminRecipients").Get<string[]>() ?? Array.Empty<string>();
                        if (adminEmails.Length > 0)
                        {
                            var descripcion = $"Pago de factura {request.NumeroFactura}";
                            var asuntoAdmin = $"💰 Nuevo Pago PayPal - {nombreClienteEmail} - ${monto} {moneda}";
                            var cuerpoAdmin = GenerarCorreoAdministrador(
                                nombreClienteEmail, 
                                emailClienteEnvio, 
                                fechaPago, 
                                monto, 
                                moneda, 
                                transactionId, 
                                "PAYPAL-AUTH", // authNumber 
                                request.OrderId, 
                                descripcion, 
                                request.NumeroFactura
                            );

                            foreach (var adminEmail in adminEmails)
                            {
                                await _emailService.SendEmailAsync(new EmailRequest
                                {
                                    To = adminEmail,
                                    Subject = asuntoAdmin,
                                    HtmlContent = cuerpoAdmin
                                });
                            }
                        }
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Error al enviar emails de confirmación PayPal");
                        // No fallar el proceso por error de email
                    }
                }

                return Ok(new {
                    success = true,
                    message = "Pago capturado exitosamente",
                    data = new {
                        transactionId = transactionId,
                        orderId = request.OrderId,
                        status = captureResponse.Status,
                        amount = monto,
                        currency = moneda,
                        payer = new {
                            name = $"{captureResponse.Payer?.Name?.GivenName} {captureResponse.Payer?.Name?.Surname}".Trim(),
                            email = captureResponse.Payer?.EmailAddress,
                            payerId = captureResponse.Payer?.PayerId
                        },
                        captureTime = capture?.CreateTime,
                        numeroFactura = request.NumeroFactura,
                        receiptNumber = serNrRegistrado // Agregar el número de recibo
                    }
                });
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Error de red al capturar pago PayPal. OrderId: {OrderId}", request?.OrderId);
                return StatusCode(503, new {
                    success = false,
                    message = "Error de conexión con PayPal. Por favor, intente nuevamente.",
                    errorCode = "NETWORK_ERROR",
                    orderId = request?.OrderId
                });
            }
            catch (TaskCanceledException tcEx)
            {
                _logger.LogError(tcEx, "Timeout al capturar pago PayPal. OrderId: {OrderId}", request?.OrderId);
                return StatusCode(504, new {
                    success = false,
                    message = "La solicitud a PayPal tardó demasiado tiempo. Por favor, intente nuevamente.",
                    errorCode = "TIMEOUT_ERROR",
                    orderId = request?.OrderId
                });
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Error al parsear respuesta de PayPal. OrderId: {OrderId}", request?.OrderId);
                return StatusCode(500, new {
                    success = false,
                    message = "Error al procesar la respuesta de PayPal",
                    errorCode = "PARSE_ERROR",
                    orderId = request?.OrderId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al capturar pago PayPal. OrderId: {OrderId}, Error: {ErrorMessage}", 
                    request?.OrderId, ex.Message);
                return StatusCode(500, new {
                    success = false,
                    message = "Error interno del servidor al procesar el pago",
                    errorCode = "INTERNAL_ERROR",
                    orderId = request?.OrderId,
                    details = _configuration["Environment:ShowErrors"] == "true" ? ex.Message : null
                });
            }
        }

        /// <summary>
        /// Obtiene un token de acceso de PayPal
        /// </summary>
        /// <returns>Token de acceso</returns>
        private async Task<string> GetPayPalAccessToken()
        {
            try
            {
                // Validar configuración
                if (string.IsNullOrEmpty(_payPalSettings.ClientId) || string.IsNullOrEmpty(_payPalSettings.ClientSecret))
                {
                    _logger.LogError("PayPal ClientId o ClientSecret no están configurados");
                    return string.Empty;
                }

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                
                // Configurar autenticación básica con client ID y secret
                var authValue = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{_payPalSettings.ClientId}:{_payPalSettings.ClientSecret}")
                );
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                
                // Preparar solicitud
                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                // Construir URL correctamente
                string tokenUrl;
                if (!string.IsNullOrEmpty(_payPalSettings.BaseUrl))
                {
                    // Si BaseUrl está configurado, usarlo con el endpoint
                    tokenUrl = _payPalSettings.BaseUrl.TrimEnd('/') + "/" + _payPalSettings.TokenEndpoint.TrimStart('/');
                }
                else
                {
                    // Si no hay BaseUrl, usar producción por defecto
                    _logger.LogWarning("PayPal BaseUrl no configurado, usando producción por defecto");
                    tokenUrl = "https://api-m.paypal.com/v1/oauth2/token";
                }
                    
                _logger.LogInformation("Intentando obtener token PayPal desde: {TokenUrl}, Mode: {Mode}", 
                    tokenUrl, _payPalSettings.Mode);

                var response = await httpClient.PostAsync(tokenUrl, formContent);
                var tokenContent = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error al obtener token PayPal: StatusCode={StatusCode}, Content={Content}, URL={Url}, ClientId={ClientId}", 
                        response.StatusCode, tokenContent, tokenUrl, _payPalSettings.ClientId);
                    
                    // Intentar parsear el error de PayPal
                    try
                    {
                        var errorObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(tokenContent);
                        if (errorObj.TryGetProperty("error", out var error))
                        {
                            var errorDescription = errorObj.TryGetProperty("error_description", out var desc) 
                                ? desc.GetString() 
                                : "Sin descripción";
                            _logger.LogError("PayPal Auth Error: {Error} - {Description}", 
                                error.GetString(), errorDescription);
                        }
                    }
                    catch
                    {
                        // Si no se puede parsear el error, ya está loggeado arriba
                    }
                    
                    return string.Empty;
                }

                var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<PayPalTokenResponse>(tokenContent);
                
                if (tokenResponse?.AccessToken == null)
                {
                    _logger.LogError("Token PayPal recibido pero AccessToken es null. Response: {Response}", tokenContent);
                    return string.Empty;
                }
                
                _logger.LogInformation("Token PayPal obtenido exitosamente, expira en {ExpiresIn} segundos", 
                    tokenResponse.ExpiresIn);
                
                return tokenResponse.AccessToken;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Error de red al conectar con PayPal API");
                return string.Empty;
            }
            catch (TaskCanceledException tcEx)
            {
                _logger.LogError(tcEx, "Timeout al conectar con PayPal API");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al obtener token de acceso PayPal");
                return string.Empty;
            }
        }

        // Versiones duplicadas eliminadas - usar métodos de compatibilidad definidos más arriba
        
        #endregion

        /// <summary>
        /// Normaliza un monto decimal para asegurar formato correcto independientemente de la cultura.
        /// Convierte "200,00" o "2.00" al formato estándar "2.00" usando InvariantCulture.
        /// </summary>
        private string NormalizarMontoDecimal(string montoOriginal)
        {
            if (string.IsNullOrEmpty(montoOriginal))
                return "0.00";
                
            // Intentar parsear con InvariantCulture primero (formato con punto decimal)
            if (decimal.TryParse(montoOriginal, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal valor))
            {
                return valor.ToString("F2", CultureInfo.InvariantCulture);
            }
            
            // Si falla, intentar con formato de coma decimal (cultura española/europea)
            if (decimal.TryParse(montoOriginal, NumberStyles.AllowDecimalPoint, new CultureInfo("es-ES"), out valor))
            {
                return valor.ToString("F2", CultureInfo.InvariantCulture);
            }
            
            // Como último recurso, reemplazar coma por punto y parsear
            var montoConPunto = montoOriginal.Replace(',', '.');
            if (decimal.TryParse(montoConPunto, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valor))
            {
                return valor.ToString("F2", CultureInfo.InvariantCulture);
            }
            
            // Si todo falla, devolver 0.00
            return "0.00";
        }

        /// <summary>
        /// Obtiene la URL del logo según el código de la empresa
        /// </summary>
        private string GetCompanyLogoUrl(string companyCode)
        {
            return companyCode switch
            {
                "2" => "https://selfservice-dev.celero.network/lovable-uploads/Logo_Celero_Networks.svg", // Celero Networks, Corp
                "3" => "https://selfservice-dev.celero.network/lovable-uploads/Logo_Celero_Quantum.svg", // Celero Quantum, Corp
                _ => "https://selfservice-dev.celero.network/lovable-uploads/Cabecera.png" // Logo por defecto
            };
        }

        /// <summary>
        /// Genera el HTML del correo de confirmación para el cliente (Tarjeta de Crédito)
        /// Método de compatibilidad - redirige a la plantilla unificada
        /// </summary>
        private string GenerarCorreoClienteTarjeta(string nombreCliente, string fechaPago, string monto, string moneda,
            string transactionId, string authNumber, string ordenId, string descripcion, string numeroContrato, string companyCode = "2")
        {
            return GenerarCorreoClienteUnificado(nombreCliente, fechaPago, monto, moneda, transactionId, authNumber, ordenId, descripcion, numeroContrato, companyCode, "TARJETA DE CREDITO/DEBITO");
        }

        /// <summary>
        /// Genera el HTML del correo para administradores (Tarjeta de Crédito)
        /// </summary>
        private string GenerarCorreoAdministradorTarjeta(string nombreCliente, string? emailCliente, string fechaPago,
            string monto, string moneda, string transactionId, string authNumber, string ordenId, string descripcion, string numeroContrato)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Nueva Venta con Tarjeta - Celero Network</title>
</head>
<body style='margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #1a1a1a;'>
    <table cellpadding='0' cellspacing='0' width='100%' style='background-color: #1a1a1a; padding: 20px 0;'>
        <tr>
            <td align='center'>
                <table cellpadding='0' cellspacing='0' width='600' style='background-color: #2d2d2d; border-radius: 10px; border: 1px solid #444;'>
                    <!-- Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #6c5ce7 0%, #74b9ff 100%); padding: 30px 40px; border-radius: 10px 10px 0 0;'>
                            <h1 style='color: white; margin: 0; font-size: 24px; font-weight: bold; text-align: center;'>
                                💰 Nueva Venta con Tarjeta de Crédito
                            </h1>
                            <p style='color: #f8f9fa; margin: 10px 0 0 0; font-size: 16px; text-align: center;'>
                                Pago con Tarjeta de Crédito/Débito
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px;'>
                            <!-- Alert Box -->
                            <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #4a4a4a; border-radius: 8px; margin-bottom: 30px;'>
                                <tr>
                                    <td style='padding: 20px;'>
                                        <h2 style='color: #74b9ff; margin: 0 0 10px 0; font-size: 20px;'>
                                            💵 Monto Recibido: {moneda} {monto}
                                        </h2>
                                        <p style='color: #f8f9fa; margin: 0; font-size: 16px;'>
                                            Cliente: <strong>{nombreCliente}</strong>
                                        </p>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Transaction Details -->
                            <div style='background-color: #3a3a3a; border-radius: 8px; padding: 25px; margin-bottom: 30px;'>
                                <h3 style='color: #6c5ce7; margin-top: 0; font-size: 18px; margin-bottom: 15px;'>
                                    💳 Información del Pago
                                </h3>
                                <table width='100%' cellpadding='0' cellspacing='0'>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999; width: 40%;'>Fecha y Hora:</td>
                                        <td style='padding: 8px 0; color: #f8f9fa;'>{fechaPago}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999;'>Pasarela:</td>
                                        <td style='padding: 8px 0; color: #f8f9fa;'>Cobalt (Tarjeta)</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999;'>ID de Transacción:</td>
                                        <td style='padding: 8px 0; color: #74b9ff; font-family: monospace;'>{transactionId}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999;'>Número de Autorización:</td>
                                        <td style='padding: 8px 0; color: #74b9ff; font-family: monospace;'>{authNumber}</td>
                                    </tr>
                                </table>
                            </div>
                            
                            <!-- Customer Details -->
                            <div style='background-color: #3a3a3a; border-radius: 8px; padding: 25px;'>
                                <h3 style='color: #6c5ce7; margin-top: 0; font-size: 18px; margin-bottom: 15px;'>
                                    👤 Información del Cliente
                                </h3>
                                <table width='100%' cellpadding='0' cellspacing='0'>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999; width: 40%;'>Nombre:</td>
                                        <td style='padding: 8px 0; color: #f8f9fa;'>{nombreCliente}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999;'>Email:</td>
                                        <td style='padding: 8px 0; color: #74b9ff;'>{emailCliente ?? "No disponible"}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999;'>Número de Contrato:</td>
                                        <td style='padding: 8px 0; color: #f8f9fa;'>{numeroContrato}</td>
                                    </tr>
                                </table>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background-color: #2a2a2a; padding: 30px 40px; text-align: center; border-radius: 0 0 10px 10px; border-top: 1px solid #444;'>
                            <p style='color: #999; font-size: 14px; margin: 0;'>
                                Este es un correo automático del sistema de pagos de Celero Network
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        // Versión duplicada eliminada - usar método de compatibilidad definido más arriba

        /// <summary>
        /// Genera el HTML del correo para administradores (Yappy)
        /// </summary>
        private string GenerarCorreoAdministradorYappy(string nombreCliente, string? emailCliente, string fechaPago,
            string monto, string moneda, string transactionId, string orderId, string descripcion, string numeroFactura)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Nuevo Pago Yappy - Celero Network</title>
</head>
<body style='margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #1a1a1a;'>
    <table cellpadding='0' cellspacing='0' width='100%' style='background-color: #1a1a1a; padding: 20px 0;'>
        <tr>
            <td align='center'>
                <table cellpadding='0' cellspacing='0' width='600' style='background-color: #2d2d2d; border-radius: 10px; border: 1px solid #444;'>
                    <!-- Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #ff6b6b 0%, #4ecdc4 100%); padding: 30px 40px; border-radius: 10px 10px 0 0;'>
                            <h1 style='color: white; margin: 0; font-size: 24px; font-weight: bold; text-align: center;'>
                                💰 Nuevo Pago Recibido vía Yappy
                            </h1>
                            <p style='color: #f8f9fa; margin: 10px 0 0 0; font-size: 16px; text-align: center;'>
                                Pago con Yappy (Banco General)
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px;'>
                            <!-- Alert Box -->
                            <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #ff6b6b; border-radius: 8px; margin-bottom: 30px;'>
                                <tr>
                                    <td style='padding: 20px;'>
                                        <h2 style='color: white; margin: 0 0 10px 0; font-size: 20px;'>
                                            💵 Monto Recibido: {moneda} {monto}
                                        </h2>
                                        <p style='color: #fff; margin: 0; font-size: 16px;'>
                                            Cliente: <strong>{nombreCliente}</strong>
                                        </p>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Transaction Details -->
                            <div style='background-color: #3a3a3a; border-radius: 8px; padding: 25px; margin-bottom: 30px;'>
                                <h3 style='color: #4ecdc4; margin-top: 0; font-size: 18px; margin-bottom: 15px;'>
                                    📱 Información del Pago
                                </h3>
                                <table width='100%' cellpadding='0' cellspacing='0'>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999; width: 40%;'>Fecha y Hora:</td>
                                        <td style='padding: 8px 0; color: #f8f9fa;'>{fechaPago}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999;'>Pasarela:</td>
                                        <td style='padding: 8px 0; color: #f8f9fa;'>Yappy (Banco General)</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999;'>ID de Transacción:</td>
                                        <td style='padding: 8px 0; color: #4ecdc4; font-family: monospace;'>{transactionId}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999;'>ID de Orden Yappy:</td>
                                        <td style='padding: 8px 0; color: #4ecdc4; font-family: monospace;'>{orderId}</td>
                                    </tr>
                                </table>
                            </div>
                            
                            <!-- Customer Details -->
                            <div style='background-color: #3a3a3a; border-radius: 8px; padding: 25px;'>
                                <h3 style='color: #4ecdc4; margin-top: 0; font-size: 18px; margin-bottom: 15px;'>
                                    👤 Información del Cliente
                                </h3>
                                <table width='100%' cellpadding='0' cellspacing='0'>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999; width: 40%;'>Nombre:</td>
                                        <td style='padding: 8px 0; color: #f8f9fa;'>{nombreCliente}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999;'>Email:</td>
                                        <td style='padding: 8px 0; color: #4ecdc4;'>{emailCliente ?? "No disponible"}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 8px 0; color: #999;'>Número de Factura:</td>
                                        <td style='padding: 8px 0; color: #f8f9fa;'>{numeroFactura}</td>
                                    </tr>
                                </table>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background-color: #2a2a2a; padding: 30px 40px; text-align: center; border-radius: 0 0 10px 10px; border-top: 1px solid #444;'>
                            <p style='color: #999; font-size: 14px; margin: 0;'>
                                Este es un correo automático del sistema de pagos Yappy de Celero Network
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        /// <summary>
        /// Obtiene las instrucciones de pago ACH configuradas para una empresa específica
        /// </summary>
        /// <param name="companyCode">Código de la empresa (2 para Networks, 3 para Quantum)</param>
        /// <returns>Instrucciones de pago ACH</returns>
        [HttpGet("ach-instructions/{companyCode}")]
        public IActionResult GetACHInstructions(string companyCode)
        {
            try
            {
                var achSettings = _configuration.GetSection("ACHInstructions").Get<ACHInstructionsSettings>();
                
                if (achSettings == null || achSettings.Companies == null)
                {
                    return Ok(new {
                        success = false,
                        message = "No hay instrucciones ACH configuradas"
                    });
                }

                // Buscar las instrucciones para la empresa específica
                if (!achSettings.Companies.TryGetValue(companyCode, out var companyInstructions))
                {
                    // Si no hay instrucciones específicas para la empresa, devolver valores por defecto
                    return Ok(new {
                        success = true,
                        data = new {
                            title = "Instrucciones para pago ACH:",
                            steps = new[] {
                                "Realice la transferencia ACH a la cuenta bancaria de Celero",
                                "Tome una captura o foto del comprobante de transferencia",
                                "Complete los datos de la transacción arriba",
                                "Suba el comprobante usando el botón de arriba",
                                "Confirme el pago para completar el proceso"
                            },
                            bankDetails = new {
                                title = "Datos bancarios:",
                                banks = new[] {
                                    new {
                                        beneficiary = "CELERO S.A.",
                                        bank = "Banco General",
                                        accountNumber = "03-01-01-123456-7",
                                        accountType = "Cuenta Corriente"
                                    }
                                }
                            }
                        }
                    });
                }

                return Ok(new {
                    success = true,
                    data = new {
                        title = companyInstructions.Title,
                        steps = companyInstructions.Steps,
                        bankDetails = new {
                            title = companyInstructions.BankDetails.Title,
                            banks = companyInstructions.BankDetails.Banks.Select(b => new {
                                beneficiary = b.Beneficiary,
                                bank = b.Bank,
                                accountNumber = b.AccountNumber,
                                accountType = b.AccountType
                            })
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener instrucciones ACH");
                return StatusCode(500, new {
                    success = false,
                    message = "Error interno del servidor"
                });
            }
        }

        /// <summary>
        /// Obtiene las instrucciones de pago ACH (compatibilidad hacia atrás - usa empresa 2 por defecto)
        /// </summary>
        /// <returns>Instrucciones de pago ACH</returns>
        [HttpGet("ach-instructions")]
        public IActionResult GetACHInstructionsDefault()
        {
            // Por defecto retorna las instrucciones de Celero Networks (empresa 2)
            return GetACHInstructions("2");
        }

        /// <summary>
        /// Lista todas las plantillas de email disponibles para visualización
        /// </summary>
        [HttpGet("email-templates")]
        public IActionResult VisualizarPlantillas()
        {
            return Ok(new
            {
                message = "Plantillas de email disponibles para visualización",
                templates = new
                {
                    paypal = new
                    {
                        admin = "/api/clientes/email-templates/paypal-admin",
                        cliente = "/api/clientes/email-templates/paypal-cliente"
                    },
                    yappy = new
                    {
                        admin = "/api/clientes/email-templates/yappy-admin",
                        cliente = "/api/clientes/email-templates/yappy-cliente"
                    },
                    tarjeta = new
                    {
                        admin = "/api/clientes/email-templates/tarjeta-admin",
                        cliente = "/api/clientes/email-templates/tarjeta-cliente"
                    },
                    otros = new
                    {
                        welcome = "/api/clientes/email-templates/welcome",
                        reminder = "/api/clientes/email-templates/reminder"
                    }
                },
                visualizador = "/api/clientes/email-templates/viewer"
            });
        }

        /// <summary>
        /// Visualiza la plantilla de email para administradores (PayPal)
        /// </summary>
        [HttpGet("email-templates/paypal-admin")]
        public IActionResult VisualizarPlantillaPayPalAdmin()
        {
            var html = GenerarCorreoAdministrador(
                nombreCliente: "Juan Pérez González", 
                emailCliente: "juan.perez@ejemplo.com",
                fechaPago: DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                monto: "150.75",
                moneda: "USD",
                transactionId: "PAY-1A123456789012345",
                authNumber: "PAYPAL-AUTH",
                ordenId: "ORDER-123456789",
                descripcion: "Pago de factura #INV-2025-001",
                numeroContrato: "INV-2025-001"
            );
            
            return Content(html, "text/html");
        }

        /// <summary>
        /// Visualiza la plantilla de email para clientes (PayPal)
        /// </summary>
        [HttpGet("email-templates/paypal-cliente")]
        public IActionResult VisualizarPlantillaPayPalCliente()
        {
            var html = GenerarCorreoClientePayPal(
                nombreCliente: "Juan Pérez González",
                fechaPago: DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                monto: "150.75",
                moneda: "USD",
                transactionId: "PAY-1A123456789012345",
                authNumber: "PAYPAL-AUTH",
                ordenId: "ORDER-123456789",
                descripcion: "Pago de factura #INV-2025-001",
                numeroContrato: "INV-2025-001"
            );
            
            return Content(html, "text/html");
        }

        /// <summary>
        /// Visualiza la plantilla de email para administradores (Yappy)
        /// </summary>
        [HttpGet("email-templates/yappy-admin")]
        public IActionResult VisualizarPlantillaYappyAdmin()
        {
            var html = GenerarCorreoAdministradorYappy(
                nombreCliente: "María García López", 
                emailCliente: "maria.garcia@ejemplo.com",
                fechaPago: DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                monto: "89.50",
                moneda: "USD",
                transactionId: "YAPPY-987654321",
                orderId: "YAP-123456789",
                descripcion: "Pago de factura #INV-2025-002",
                numeroFactura: "INV-2025-002"
            );
            
            return Content(html, "text/html");
        }

        /// <summary>
        /// Visualiza la plantilla de email para clientes (Yappy)
        /// </summary>
        [HttpGet("email-templates/yappy-cliente")]
        public IActionResult VisualizarPlantillaYappyCliente()
        {
            var html = GenerarCorreoClienteYappy(
                nombreCliente: "María García López",
                fechaPago: DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                monto: "89.50",
                moneda: "USD",
                transactionId: "YAPPY-987654321",
                authNumber: "YAPPY-AUTH",
                ordenId: "YAP-123456789",
                descripcion: "Pago de factura #INV-2025-002",
                numeroContrato: "INV-2025-002"
            );
            
            return Content(html, "text/html");
        }

        /// <summary>
        /// Visualiza la plantilla de email para administradores (Tarjeta de Crédito)
        /// </summary>
        [HttpGet("email-templates/tarjeta-admin")]
        public IActionResult VisualizarPlantillaTarjetaAdmin()
        {
            var html = GenerarCorreoAdministradorTarjeta(
                nombreCliente: "Carlos López Martínez", 
                emailCliente: "carlos.lopez@ejemplo.com",
                fechaPago: DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                monto: "225.00",
                moneda: "USD",
                transactionId: "CC-456789123456",
                authNumber: "AUTH123456",
                ordenId: "ORD-789123456",
                descripcion: "Pago de factura #INV-2025-003",
                numeroContrato: "INV-2025-003"
            );
            
            return Content(html, "text/html");
        }

        /// <summary>
        /// Visualiza la plantilla de email para clientes (Tarjeta de Crédito)
        /// </summary>
        [HttpGet("email-templates/tarjeta-cliente")]
        public IActionResult VisualizarPlantillaTarjetaCliente()
        {
            var html = GenerarCorreoClienteTarjeta(
                nombreCliente: "Carlos López Martínez",
                fechaPago: DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                monto: "225.00",
                moneda: "USD",
                transactionId: "CC-456789123456",
                authNumber: "AUTH123456",
                ordenId: "ORD-789123456",
                descripcion: "Pago de factura #INV-2025-003",
                numeroContrato: "INV-2025-003",
                companyCode: "2"
            );
            
            return Content(html, "text/html");
        }

        /// <summary>
        /// Página HTML para navegar todas las plantillas
        /// </summary>
        [HttpGet("email-templates/viewer")]
        public IActionResult VisualizadorPlantillas()
        {
            var html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Visualizador de Plantillas Email - Celero Network</title>
    <style>
        body { 
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
            margin: 0; 
            padding: 20px; 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
        }
        .container { 
            max-width: 1200px; 
            margin: 0 auto; 
        }
        .header { 
            background: rgba(255, 255, 255, 0.95);
            color: #333; 
            padding: 30px; 
            border-radius: 15px; 
            margin-bottom: 30px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.1);
            text-align: center;
        }
        .header h1 {
            margin: 0 0 10px 0;
            font-size: 2.5em;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
        }
        .grid { 
            display: grid; 
            grid-template-columns: repeat(auto-fit, minmax(350px, 1fr)); 
            gap: 25px; 
        }
        .card { 
            background: rgba(255, 255, 255, 0.95);
            border-radius: 15px; 
            padding: 25px; 
            box-shadow: 0 10px 30px rgba(0,0,0,0.1);
            transition: transform 0.3s ease, box-shadow 0.3s ease;
        }
        .card:hover {
            transform: translateY(-5px);
            box-shadow: 0 20px 40px rgba(0,0,0,0.15);
        }
        .card h3 {
            margin: 0 0 15px 0;
            font-size: 1.4em;
            color: #333;
        }
        .card p {
            color: #666;
            line-height: 1.6;
            margin-bottom: 20px;
        }
        .btn { 
            display: inline-block; 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white; 
            padding: 12px 25px; 
            text-decoration: none; 
            border-radius: 8px; 
            margin: 5px 5px 5px 0;
            transition: all 0.3s ease;
            font-weight: 500;
        }
        .btn:hover { 
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(102, 126, 234, 0.4);
        }
        .category {
            background: #f8f9fa;
            border-left: 4px solid #667eea;
            padding: 15px;
            margin-bottom: 20px;
            border-radius: 0 8px 8px 0;
        }
        .category h4 {
            margin: 0 0 10px 0;
            color: #667eea;
            font-size: 1.1em;
        }
        .api-info {
            background: linear-gradient(135deg, #28a745 0%, #20c997 100%);
            color: white;
            padding: 25px;
            border-radius: 15px;
            margin-top: 30px;
            text-align: center;
        }
        .api-info h3 {
            margin: 0 0 15px 0;
            color: white;
        }
        .api-info .btn {
            background: rgba(255, 255, 255, 0.2);
            border: 2px solid rgba(255, 255, 255, 0.3);
        }
        .api-info .btn:hover {
            background: rgba(255, 255, 255, 0.3);
            border-color: rgba(255, 255, 255, 0.5);
        }
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎨 Visualizador de Plantillas Email</h1>
            <p>Previsualize todas las plantillas de email de Resend antes de enviarlas</p>
            <p><small>Celero Network - API de Gestión de Pagos</small></p>
        </div>

        <div class='grid'>
            <!-- PayPal Templates -->
            <div class='card'>
                <div class='category'>
                    <h4>💳 PayPal</h4>
                    <p>Plantillas para pagos procesados vía PayPal</p>
                </div>
                <h3>Notificaciones PayPal</h3>
                <p>Emails automáticos enviados cuando se procesan pagos a través de PayPal, tanto para administradores como clientes.</p>
                <a href='/api/clientes/email-templates/paypal-admin' class='btn' target='_blank'>👨‍💼 Admin</a>
                <a href='/api/clientes/email-templates/paypal-cliente' class='btn' target='_blank'>👤 Cliente</a>
            </div>

            <!-- Yappy Templates -->
            <div class='card'>
                <div class='category'>
                    <h4>📱 Yappy</h4>
                    <p>Plantillas para pagos procesados vía Yappy (Banco General)</p>
                </div>
                <h3>Notificaciones Yappy</h3>
                <p>Emails automáticos enviados cuando se procesan pagos a través de la plataforma Yappy de Banco General.</p>
                <a href='/api/clientes/email-templates/yappy-admin' class='btn' target='_blank'>👨‍💼 Admin</a>
                <a href='/api/clientes/email-templates/yappy-cliente' class='btn' target='_blank'>👤 Cliente</a>
            </div>

            <!-- Tarjeta de Crédito Templates -->
            <div class='card'>
                <div class='category'>
                    <h4>💳 Tarjeta de Crédito</h4>
                    <p>Plantillas para pagos con tarjeta vía Cobalt</p>
                </div>
                <h3>Notificaciones Tarjeta</h3>
                <p>Emails automáticos enviados cuando se procesan pagos con tarjeta de crédito a través de la plataforma Cobalt.</p>
                <a href='/api/clientes/email-templates/tarjeta-admin' class='btn' target='_blank'>👨‍💼 Admin</a>
                <a href='/api/clientes/email-templates/tarjeta-cliente' class='btn' target='_blank'>👤 Cliente</a>
            </div>

            <!-- Welcome & Reminder Templates -->
            <div class='card'>
                <div class='category'>
                    <h4>📧 Comunicaciones</h4>
                    <p>Plantillas para comunicaciones generales</p>
                </div>
                <h3>Otros Emails</h3>
                <p>Plantillas adicionales para comunicaciones como bienvenida a nuevos clientes y recordatorios de facturas vencidas.</p>
                <a href='#' class='btn' onclick='alert(""Plantilla en desarrollo"")'>🎉 Bienvenida</a>
                <a href='#' class='btn' onclick='alert(""Plantilla en desarrollo"")'>⏰ Recordatorio</a>
            </div>
        </div>

        <!-- API Info -->
        <div class='api-info'>
            <h3>📋 Información de API</h3>
            <p>También puedes acceder programáticamente a la lista de plantillas disponibles</p>
            <a href='/api/clientes/email-templates' class='btn' target='_blank'>Ver JSON de Plantillas</a>
        </div>
    </div>

    <script>
        // Agregar fecha y hora actual
        document.addEventListener('DOMContentLoaded', function() {
            const now = new Date();
            const dateStr = now.toLocaleDateString('es-ES', { 
                year: 'numeric', 
                month: 'long', 
                day: 'numeric',
                hour: '2-digit',
                minute: '2-digit'
            });
            
            const header = document.querySelector('.header p:last-child');
            header.innerHTML = `<small>Celero Network - API de Gestión de Pagos | ${dateStr}</small>`;
        });
    </script>
</body>
</html>";
            
            return Content(html, "text/html");
        }

        /// <summary>
        /// Registra un recibo de pago Yappy en la base de datos offline para procesamiento posterior
        /// </summary>
        private async Task RegistrarReciboYappyOffline(string orderId)
        {
            try
            {
                using (var scope = HttpContext.RequestServices.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ReciboCajaOfflineContext>();
                    
                    // Buscar la información de la orden almacenada previamente
                    var ordenInfo = await db.RecibosOffline.FirstOrDefaultAsync(r => r.SerNr == orderId && r.PayMode == "YP_PENDING");
                    
                    if (ordenInfo != null)
                    {
                        // Recuperar información de facturas del JSON almacenado
                        var ordenData = System.Text.Json.JsonSerializer.Deserialize<dynamic>(ordenInfo.DetallesJson);
                        var invoiceDetailsJson = ordenData.GetProperty("InvoiceDetails");
                        var invoiceDetails = System.Text.Json.JsonSerializer.Deserialize<List<YappyInvoiceDetail>>(invoiceDetailsJson.GetRawText());
                        var clienteCode = ordenData.GetProperty("ClienteCode").GetString();
                        var emailCliente = ordenData.GetProperty("EmailCliente").GetString();
                        
                        if (invoiceDetails != null && invoiceDetails.Count > 0)
                        {
                            // Crear el recibo usando el estándar de múltiples facturas
                            var entry = new Entry
                            {
                                SerNr = orderId,
                                TransDate = DateTime.Now.ToString("dd-MM-yyyy"),
                                PayMode = "YP", // YP para Yappy
                                Person = clienteCode,
                                CUCode = clienteCode,
                                RefStr = orderId,
                                Detalles = new List<Detalle>()
                            };
                            
                            // Crear un detalle para cada factura
                            foreach (var invoiceDetail in invoiceDetails)
                            {
                                entry.Detalles.Add(new Detalle
                                {
                                    InvoiceNr = invoiceDetail.InvoiceNumber,
                                    Sum = invoiceDetail.Amount.ToString("F2"),
                                    Objects = $"Yappy - {orderId}",
                                    Stp = "1"
                                });
                            }
                            
                            var datos = new Dictionary<string, Entry> { { entry.SerNr, entry } };
                            
                            // Registrar el recibo en Hansa
                            string? serNrRegistrado = await AgregarRecibosDeCajasAsync(datos, _hansaSettings, _logger, _hansaReceiptService);
                            
                            if (!string.IsNullOrEmpty(serNrRegistrado) && serNrRegistrado != "20878" && serNrRegistrado != "20060")
                            {
                                _logger.LogInformation("Recibo Yappy registrado exitosamente en Hansa. SerNr: {SerNr}", serNrRegistrado);
                                
                                // Enviar notificación usando el método reutilizable
                                decimal totalAmount = 0;
                                string facturasList = "";
                                
                                foreach (var detail in invoiceDetails)
                                {
                                    totalAmount += detail.Amount;
                                    if (!string.IsNullOrEmpty(facturasList)) facturasList += ", ";
                                    facturasList += detail.InvoiceNumber;
                                }
                                
                                await EnviarNotificacionReciboAsync(
                                    serNrRegistrado,
                                    emailCliente,
                                    "YAPPY", 
                                    totalAmount,
                                    facturasList,
                                    clienteCode,
                                    "Cliente Yappy" // Nombre por defecto
                                );
                                
                                // Actualizar el registro como procesado
                                ordenInfo.PayMode = "YP"; // Cambiar de YP_PENDING a YP
                                ordenInfo.Pendiente = false;
                                await db.SaveChangesAsync();
                            }
                            else
                            {
                                _logger.LogError("Error registrando recibo Yappy en Hansa. Código: {Codigo}", serNrRegistrado);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("No se encontraron detalles de facturas para la orden Yappy: {OrderId}", orderId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No se encontró información de orden Yappy: {OrderId}", orderId);
                        
                        // Crear recibo básico como fallback
                        var reciboOffline = new Models.ReciboCajaOffline
                        {
                            SerNr = orderId + "_BASIC",
                            TransDate = DateTime.Now,
                            PayMode = "YP",
                            Person = "YAPPY_CLIENT",
                            CUCode = "YAPPY_CLIENT", 
                            RefStr = orderId,
                            DetallesJson = System.Text.Json.JsonSerializer.Serialize(new 
                            { 
                                OrderId = orderId,
                                MetodoPago = "Yappy",
                                FechaProceso = DateTime.Now,
                                Nota = "Recibo básico - información de facturas no disponible"
                            }),
                            Pendiente = true
                        };
                        
                        db.RecibosOffline.Add(reciboOffline);
                        await db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar recibo Yappy. OrderId: {OrderId}", orderId);
                throw;
            }
        }

        /// <summary>
        /// Envía email de confirmación de pago Yappy usando la plantilla unificada
        /// </summary>
        private async Task EnviarEmailConfirmacionYappy(string orderId)
        {
            try
            {
                // Por ahora usar información genérica - se puede mejorar obteniendo datos reales del cliente
                var nombreCliente = "Cliente Yappy";
                var fechaPago = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                var monto = "0.00"; // Se puede obtener de la orden si se guarda esta info
                var moneda = "USD";
                var numeroContrato = orderId.Substring(0, Math.Min(8, orderId.Length));
                
                // Usar la plantilla unificada de tarjeta que detecta automáticamente el método de pago
                var html = GenerarCorreoClienteTarjeta(
                    nombreCliente: nombreCliente,
                    fechaPago: fechaPago,
                    monto: monto,
                    moneda: moneda,
                    transactionId: orderId,
                    authNumber: "YAPPY-AUTH",
                    ordenId: orderId,
                    descripcion: $"Pago procesado vía Yappy",
                    numeroContrato: numeroContrato
                );

                // Configurar el email usando el servicio existente
                var emailRequest = new EmailRequest
                {
                    To = "cliente@ejemplo.com", // TODO: Obtener email real del cliente
                    Subject = $"CELERO - Confirmación de Pago / Recibo {numeroContrato}",
                    HtmlContent = html,
                    From = "noreply@celero.net",
                    FromName = "CELERO"
                };

                // Enviar email usando el servicio existente
                var result = await _emailService.SendEmailAsync(emailRequest);
                
                if (result.Success)
                {
                    _logger.LogInformation("Email de confirmación Yappy enviado exitosamente. OrderId: {OrderId}", orderId);
                }
                else
                {
                    _logger.LogError("Error al enviar email de confirmación Yappy. OrderId: {OrderId}, Error: {Error}", orderId, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar email de confirmación Yappy. OrderId: {OrderId}", orderId);
                throw;
            }
        }

    }
}