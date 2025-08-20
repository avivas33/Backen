using System.Text.Json.Serialization;

namespace Api_Celero.Models
{
    /// <summary>
    /// Solicitud para crear una orden de pago con PayPal
    /// </summary>
    public class PayPalCreateOrderRequest
    {
        public string ClienteCode { get; set; } = string.Empty;
        public string NumeroFactura { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string Description { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
        public string? EmailCliente { get; set; }
        public string? NombreCliente { get; set; }
        
        /// <summary>
        /// Detalles específicos de cada factura con su monto exacto.
        /// Se usa para pagos de múltiples facturas.
        /// </summary>
        public List<PayPalInvoiceDetail>? InvoiceDetails { get; set; }
    }

    /// <summary>
    /// Respuesta de PayPal al crear una orden
    /// </summary>
    public class PayPalCreateOrderResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("links")]
        public List<PayPalLink> Links { get; set; } = new List<PayPalLink>();
    }

    /// <summary>
    /// Enlaces devueltos por PayPal
    /// </summary>
    public class PayPalLink
    {
        [JsonPropertyName("href")]
        public string Href { get; set; } = string.Empty;

        [JsonPropertyName("rel")]
        public string Rel { get; set; } = string.Empty;

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;
    }

    /// <summary>
    /// Solicitud para capturar un pago de PayPal
    /// </summary>
    public class PayPalCaptureRequest
    {
        public string OrderId { get; set; } = string.Empty;
        public string ClienteCode { get; set; } = string.Empty;
        
        [Obsolete("Use InvoiceDetails instead. This field is kept for backward compatibility only.")]
        public string NumeroFactura { get; set; } = string.Empty;
        
        /// <summary>
        /// Email del cliente del ERP (no de PayPal)
        /// </summary>
        public string? EmailCliente { get; set; }
        
        /// <summary>
        /// REQUERIDO: Detalles específicos de cada factura con su monto exacto.
        /// Formato estándar para una o múltiples facturas.
        /// </summary>
        public List<PayPalInvoiceDetail> InvoiceDetails { get; set; } = new List<PayPalInvoiceDetail>();
    }

    /// <summary>
    /// Detalle de factura con monto específico para PayPal
    /// </summary>
    public class PayPalInvoiceDetail
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// Respuesta de PayPal al capturar un pago
    /// </summary>
    public class PayPalCaptureResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("purchase_units")]
        public List<PayPalPurchaseUnit> PurchaseUnits { get; set; } = new List<PayPalPurchaseUnit>();

        [JsonPropertyName("payer")]
        public PayPalPayer? Payer { get; set; }
    }

    /// <summary>
    /// Unidad de compra en PayPal
    /// </summary>
    public class PayPalPurchaseUnit
    {
        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public PayPalAmount Amount { get; set; } = new PayPalAmount();

        [JsonPropertyName("payments")]
        public PayPalPayments? Payments { get; set; }
    }

    /// <summary>
    /// Monto en PayPal
    /// </summary>
    public class PayPalAmount
    {
        [JsonPropertyName("currency_code")]
        public string CurrencyCode { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Pagos en PayPal
    /// </summary>
    public class PayPalPayments
    {
        [JsonPropertyName("captures")]
        public List<PayPalCapture> Captures { get; set; } = new List<PayPalCapture>();
    }

    /// <summary>
    /// Captura de pago en PayPal
    /// </summary>
    public class PayPalCapture
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public PayPalAmount Amount { get; set; } = new PayPalAmount();

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; } = string.Empty;
    }

    /// <summary>
    /// Información del pagador en PayPal
    /// </summary>
    public class PayPalPayer
    {
        [JsonPropertyName("name")]
        public PayPalPayerName? Name { get; set; }

        [JsonPropertyName("email_address")]
        public string EmailAddress { get; set; } = string.Empty;

        [JsonPropertyName("payer_id")]
        public string PayerId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Nombre del pagador en PayPal
    /// </summary>
    public class PayPalPayerName
    {
        [JsonPropertyName("given_name")]
        public string GivenName { get; set; } = string.Empty;

        [JsonPropertyName("surname")]
        public string Surname { get; set; } = string.Empty;
    }

    /// <summary>
    /// Token de acceso de PayPal
    /// </summary>
    public class PayPalTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    /// <summary>
    /// Cuerpo de la solicitud para crear orden en PayPal API
    /// </summary>
    internal class PayPalApiCreateOrderRequest
    {
        [JsonPropertyName("intent")]
        public string Intent { get; set; } = "CAPTURE";

        [JsonPropertyName("purchase_units")]
        public List<PayPalApiPurchaseUnit> PurchaseUnits { get; set; } = new List<PayPalApiPurchaseUnit>();

        [JsonPropertyName("payment_source")]
        public PayPalApiPaymentSource? PaymentSource { get; set; }

        [JsonPropertyName("application_context")]
        public PayPalApiApplicationContext ApplicationContext { get; set; } = new PayPalApiApplicationContext();
    }

    /// <summary>
    /// Unidad de compra para API de PayPal
    /// </summary>
    internal class PayPalApiPurchaseUnit
    {
        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public PayPalApiAmount Amount { get; set; } = new PayPalApiAmount();
    }

    /// <summary>
    /// Monto para API de PayPal
    /// </summary>
    internal class PayPalApiAmount
    {
        [JsonPropertyName("currency_code")]
        public string CurrencyCode { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Fuente de pago para API de PayPal
    /// </summary>
    internal class PayPalApiPaymentSource
    {
        [JsonPropertyName("paypal")]
        public PayPalApiPaypal Paypal { get; set; } = new PayPalApiPaypal();
    }

    /// <summary>
    /// Configuración de PayPal para la fuente de pago
    /// </summary>
    internal class PayPalApiPaypal
    {
        [JsonPropertyName("experience_context")]
        public PayPalApiExperienceContext ExperienceContext { get; set; } = new PayPalApiExperienceContext();
    }

    /// <summary>
    /// Contexto de experiencia para PayPal
    /// </summary>
    internal class PayPalApiExperienceContext
    {
        [JsonPropertyName("payment_method_preference")]
        public string PaymentMethodPreference { get; set; } = "IMMEDIATE_PAYMENT_REQUIRED";

        [JsonPropertyName("return_url")]
        public string ReturnUrl { get; set; } = string.Empty;

        [JsonPropertyName("cancel_url")]
        public string CancelUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Contexto de aplicación para PayPal
    /// </summary>
    internal class PayPalApiApplicationContext
    {
        [JsonPropertyName("return_url")]
        public string ReturnUrl { get; set; } = string.Empty;

        [JsonPropertyName("cancel_url")]
        public string CancelUrl { get; set; } = string.Empty;

        [JsonPropertyName("brand_name")]
        public string BrandName { get; set; } = "Celero Network";

        [JsonPropertyName("landing_page")]
        public string LandingPage { get; set; } = "LOGIN";

        [JsonPropertyName("user_action")]
        public string UserAction { get; set; } = "PAY_NOW";
    }
}