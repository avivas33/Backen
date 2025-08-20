using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Api_Celero.Models
{
    // Modelo para la respuesta de la API de Hansa IPVc (recibos)
    public class HansaReceiptResponse
    {
        [JsonProperty("data")]
        public HansaReceiptData Data { get; set; }
    }

    public class HansaReceiptData
    {
        [JsonProperty("@register")]
        public string Register { get; set; }

        [JsonProperty("@sequence")]
        public string Sequence { get; set; }

        [JsonProperty("@systemversion")]
        public string SystemVersion { get; set; }

        [JsonProperty("@linkid")]
        public string LinkId { get; set; }

        [JsonProperty("@sort")]
        public string Sort { get; set; }

        [JsonProperty("@key")]
        public string Key { get; set; }

        [JsonProperty("@range")]
        public string Range { get; set; }

        [JsonProperty("IPVc")]
        public List<IPVcReceipt> IPVc { get; set; }
    }

    public class IPVcReceipt
    {
        [JsonProperty("@register")]
        public string Register { get; set; }

        [JsonProperty("@sequence")]
        public string Sequence { get; set; }

        [JsonProperty("@url")]
        public string Url { get; set; }

        [JsonProperty("SerNr")]
        public string SerNr { get; set; } // Número de recibo

        [JsonProperty("CurPayVal")]
        public string CurPayVal { get; set; } // Total pagado

        [JsonProperty("TransDate")]
        public string TransDate { get; set; } // Fecha de transacción

        [JsonProperty("PayMode")]
        public string PayMode { get; set; } // Método de pago

        [JsonProperty("rows")]
        public List<IPVcReceiptRow> Rows { get; set; }
    }

    public class IPVcReceiptRow
    {
        [JsonProperty("@rownumber")]
        public string RowNumber { get; set; }

        [JsonProperty("InvoiceNr")]
        public string InvoiceNr { get; set; } // Referencia

        [JsonProperty("CustName")]
        public string CustName { get; set; } // Nombre del cliente

        [JsonProperty("RecVal")]
        public string RecVal { get; set; } // Monto recibido

        [JsonProperty("InvoiceOfficialSerNr")]
        public string InvoiceOfficialSerNr { get; set; } // Nro. CUFE
    }

    // Modelo para la solicitud de envío de correo de recibo
    public class ReceiptEmailRequest
    {
        public string ReceiptNumber { get; set; }
        public string CompanyCode { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerName { get; set; }
        public string TransactionDate { get; set; }
        public string PaymentMethod { get; set; }
        public decimal TotalAmount { get; set; }
        public List<ReceiptDetailItem> Details { get; set; }
    }

    public class ReceiptDetailItem
    {
        public string Reference { get; set; } // InvoiceNr
        public string CufeNumber { get; set; } // InvoiceOfficialSerNr
        public string Quota { get; set; } // Siempre será "0" según indicaste
        public decimal ReceivedAmount { get; set; } // RecVal
    }

    // Modelo para la solicitud al endpoint
    public class SendReceiptEmailRequest
    {
        public string ReceiptNumber { get; set; }
        public string CompanyCode { get; set; }
        public string CustomerEmail { get; set; }
    }
}