using System.Collections.Generic;

namespace Api_Celero.Models
{
    public class FacturaAbiertaResponse
    {
        public FacturaAbiertaData data { get; set; }
    }

    public class FacturaAbiertaData
    {
        public string @register { get; set; }
        public string @sequence { get; set; }
        public string @systemversion { get; set; }
        public string @linkid { get; set; }
        public string @sort { get; set; }
        public string @key { get; set; }
        public string @range { get; set; }
        public List<FacturaAbierta> ARVc { get; set; }
    }

    public class FacturaAbierta
    {
        public string @register { get; set; }
        public string @sequence { get; set; }
        public string @url { get; set; }
        public string InvoiceNr { get; set; }
        public string BookRVal { get; set; }
    }
}
