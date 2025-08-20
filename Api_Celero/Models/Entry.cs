namespace Api_Celero.Models
{
    public class Entry
    {
        public string SerNr { get; set; } = string.Empty;
        public string TransDate { get; set; } = string.Empty;
        public string PayMode { get; set; } = string.Empty;
        public string Person { get; set; } = string.Empty;
        public string CUCode { get; set; } = string.Empty;
        public string RefStr { get; set; } = string.Empty;
        public List<Detalle> Detalles { get; set; } = new List<Detalle>();
    }

    public class Detalle
    {
        public string InvoiceNr { get; set; } = string.Empty;
        public string Sum { get; set; } = string.Empty;
        public string Objects { get; set; } = string.Empty;
        public string Stp { get; set; } = string.Empty;
    }
}