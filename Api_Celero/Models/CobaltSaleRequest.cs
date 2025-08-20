namespace Api_Celero.Models
{
    /// <summary>
    /// Modelo para recibir datos del frontend - incluye campos adicionales para notificaciones
    /// </summary>
    public class CobaltSaleRequest
    {
        // Campos requeridos por Cobalt API
        public string currency_code { get; set; } = "USD";
        public string amount { get; set; } = string.Empty;
        public string pan { get; set; } = string.Empty;
        public string exp_date { get; set; } = string.Empty;
        public string? tax { get; set; } = "0";
        public string? tip { get; set; } = "0";
        public string? card_holder { get; set; }
          // Campos adicionales para notificaciones (NO se envían a Cobalt)
        public string? customer_email { get; set; }
        public string? customer_name { get; set; }
        public string? order_id { get; set; }
        public string? description { get; set; }
        public string? contract_number { get; set; }
        public string? company_code { get; set; } // Código de la empresa para determinar credenciales
        
        /// <summary>
        /// REQUERIDO: Detalles específicos de cada factura con su monto exacto.
        /// Formato estándar para una o múltiples facturas.
        /// </summary>
        public List<CobaltInvoiceDetail> InvoiceDetails { get; set; } = new List<CobaltInvoiceDetail>();
        
        /// <summary>
        /// Convierte el request completo al formato exacto requerido por Cobalt API
        /// </summary>
        public CobaltApiRequest ToCobaltApiFormat()
        {
            return new CobaltApiRequest
            {
                currency_code = this.currency_code,
                amount = this.amount,
                tax = this.tax ?? "0",
                tip = this.tip ?? "0", 
                pan = this.pan,
                exp_date = this.exp_date,
                card_holder = this.card_holder ?? ""
            };
        }
    }

    /// <summary>
    /// Detalle de factura con monto específico para Cobalt (tarjetas de crédito)
    /// </summary>
    public class CobaltInvoiceDetail
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
    
    /// <summary>
    /// Modelo exacto para enviar a Cobalt API (solo campos requeridos)
    /// </summary>
    public class CobaltApiRequest
    {
        public string currency_code { get; set; } = "USD";
        public string amount { get; set; } = string.Empty;
        public string tax { get; set; } = "0";
        public string tip { get; set; } = "0";
        public string pan { get; set; } = string.Empty;
        public string exp_date { get; set; } = string.Empty;
        public string card_holder { get; set; } = string.Empty;
    }

    public class CobaltSaleResponse
    {
        public string status { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public CobaltSaleData? data { get; set; }
    }

    public class CobaltSaleData
    {
        public int id { get; set; }
        public int identifier { get; set; }
        public int service_id { get; set; }
        public string merchant_id { get; set; } = string.Empty;
        public string terminal_id { get; set; } = string.Empty;
        public string processor { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string ballot { get; set; } = string.Empty;
        public string pan { get; set; } = string.Empty;
        public string exp_date { get; set; } = string.Empty;
        public string currency_code { get; set; } = string.Empty;
        public string card_holder { get; set; } = string.Empty;
        public int amount { get; set; }
        public int tax { get; set; }
        public string? response_code { get; set; }
        public string? authorization_number { get; set; }
        public string? reference_number { get; set; }
        public string? processed_at { get; set; }
        public string? created_at { get; set; }
        public string? updated_at { get; set; }
    }

    public class CobaltTokenRequest
    {
        public string grant_type { get; set; } = "client_credentials";
        public string client_id { get; set; } = string.Empty;
        public string client_secret { get; set; } = string.Empty;
    }

    public class CobaltTokenResponse
    {
        public string token_type { get; set; } = string.Empty;
        public int expires_in { get; set; }
        public string access_token { get; set; } = string.Empty;
    }
}
