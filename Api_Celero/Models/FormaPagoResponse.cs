using System.Collections.Generic;

namespace Api_Celero.Models
{
    public class FormaPagoResponse
    {
        public FormaPagoData data { get; set; }
    }

    public class FormaPagoData
    {
        public List<FormaPago> PDVc { get; set; }
    }

    public class FormaPago
    {
        public string Code { get; set; }
        public string pdComment { get; set; }
        public string Installment { get; set; }
        public string PDType { get; set; }
    }
}
