using System.Collections.Generic;

namespace Api_Celero.Models
{
    public class FacturaResponse
    {
        public FacturaData data { get; set; }
    }

    public class FacturaData
    {
        public string @register { get; set; }
        public string @sequence { get; set; }
        public string @systemversion { get; set; }
        public string @linkid { get; set; }
        public string @sort { get; set; }
        public string @key { get; set; }
        public string @range { get; set; }
        public List<Factura> IVVc { get; set; }
    }

    public class Factura
    {
        public string @register { get; set; }
        public string @sequence { get; set; }
        public string @url { get; set; }
        public string SerNr { get; set; }
        public string InvDate { get; set; }
        public string PayDate { get; set; }
        public string PayDeal { get; set; }
        public string Sum4 { get; set; }
        public string Sum3 { get; set; }
        public string OfficialSerNr { get; set; }
        public string OrderNr { get; set; }
        public string CustOrdNr { get; set; }
        public string RefStr { get; set; }
    }
}
