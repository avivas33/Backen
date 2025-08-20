namespace Api_Celero.Models
{
    /// <summary>
    /// Configuración para el proveedor de pagos Cobalt
    /// </summary>
    public class CobaltSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string TokenEndpoint { get; set; } = string.Empty;
        public string SaleEndpoint { get; set; } = string.Empty;
        
        /// <summary>
        /// Configuración de credenciales por empresa
        /// </summary>
        public Dictionary<string, CobaltCompanyCredentials> Companies { get; set; } = new();

        /// <summary>
        /// URL completa para obtener tokens
        /// </summary>
        public string TokenUrl => BaseUrl + TokenEndpoint;

        /// <summary>
        /// URL completa para procesar ventas
        /// </summary>
        public string SaleUrl => BaseUrl + SaleEndpoint;
        
        /// <summary>
        /// Obtiene las credenciales para una empresa específica
        /// </summary>
        public CobaltCompanyCredentials? GetCredentials(string companyCode)
        {
            if (Companies == null)
            {
                Console.WriteLine($"ERROR: Companies dictionary is null for company code: {companyCode}");
                return null;
            }
            
            Console.WriteLine($"Looking for company code: {companyCode}, Available companies: {string.Join(", ", Companies.Keys)}");
            
            var found = Companies.TryGetValue(companyCode, out var credentials);
            if (!found)
            {
                Console.WriteLine($"ERROR: No credentials found for company code: {companyCode}");
            }
            else
            {
                Console.WriteLine($"SUCCESS: Credentials found for company code: {companyCode}");
            }
            
            return found ? credentials : null;
        }
    }

    /// <summary>
    /// Credenciales de Cobalt específicas por empresa
    /// </summary>
    public class CobaltCompanyCredentials
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuración para el proveedor de pagos Yappy
    /// </summary>
    public class YappySettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ValidateMerchantEndpoint { get; set; } = string.Empty;
        public string CreateOrderEndpoint { get; set; } = string.Empty;
        public string MerchantId { get; set; } = string.Empty;

        /// <summary>
        /// URL completa para validar comerciante
        /// </summary>
        public string ValidateMerchantUrl => BaseUrl + ValidateMerchantEndpoint;

        /// <summary>
        /// URL completa para crear órdenes
        /// </summary>
        public string CreateOrderUrl => BaseUrl + CreateOrderEndpoint;
    }

    /// <summary>
    /// Configuración para el proveedor de pagos PayPal
    /// </summary>
    public class PayPalSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Mode { get; set; } = "sandbox"; // sandbox o live
        public string TokenEndpoint { get; set; } = "/v1/oauth2/token";
        public string OrdersEndpoint { get; set; } = "/v2/checkout/orders";
        public string CaptureEndpoint { get; set; } = "/v2/checkout/orders/{order_id}/capture";

        /// <summary>
        /// URL completa para obtener tokens
        /// </summary>
        public string TokenUrl => BaseUrl + TokenEndpoint;

        /// <summary>
        /// URL completa para crear órdenes
        /// </summary>
        public string OrdersUrl => BaseUrl + OrdersEndpoint;

        /// <summary>
        /// URL completa para capturar pagos
        /// </summary>
        public string GetCaptureUrl(string orderId) => BaseUrl + CaptureEndpoint.Replace("{order_id}", orderId);
    }
}
