namespace Api_Celero.Models
{
    public class YappyOrdenRequest
    {
        public string orderId { get; set; } = string.Empty;
        public string domain { get; set; } = string.Empty;
        public long paymentDate { get; set; }
        public string aliasYappy { get; set; } = string.Empty;
        public string ipnUrl { get; set; } = string.Empty;
        public string discount { get; set; } = "0.00";
        public string taxes { get; set; } = "0.00";
        public string subtotal { get; set; } = "0.00";
        public string total { get; set; } = "0.00";
        
        /// <summary>
        /// REQUERIDO: Detalles específicos de cada factura con su monto exacto.
        /// Formato estándar para una o múltiples facturas.
        /// </summary>
        public List<YappyInvoiceDetail> InvoiceDetails { get; set; } = new List<YappyInvoiceDetail>();
        
        /// <summary>
        /// Código del cliente para notificaciones
        /// </summary>
        public string ClienteCode { get; set; } = string.Empty;
        
        /// <summary>
        /// Email del cliente para notificaciones  
        /// </summary>
        public string EmailCliente { get; set; } = string.Empty;
    }

    /// <summary>
    /// Detalle de factura con monto específico para Yappy
    /// </summary>
    public class YappyInvoiceDetail
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// Solicitud del frontend para crear orden Yappy con estándar de facturas
    /// </summary>
    public class YappyFrontendRequest
    {
        public string yappyPhone { get; set; } = string.Empty;
        public string ClienteCode { get; set; } = string.Empty;
        public string EmailCliente { get; set; } = string.Empty;
        
        /// <summary>
        /// REQUERIDO: Detalles específicos de cada factura con su monto exacto.
        /// </summary>
        public List<YappyInvoiceDetail> InvoiceDetails { get; set; } = new List<YappyInvoiceDetail>();
    }

    public class YappyValidarResponse
    {
        public YappyStatus? status { get; set; }
        public YappyValidarBody? body { get; set; }
    }
    public class YappyStatus
    {
        public string? code { get; set; }
        public string? description { get; set; }
    }
    public class YappyValidarBody
    {
        public long epochTime { get; set; }
        public string? token { get; set; }
    }
}
