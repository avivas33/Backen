namespace Api_Celero.Models
{
    public class ClienteResponse
    {
        public ClienteData? data { get; set; }
    }

    public class ClienteData
    {
        public string? register { get; set; }
        public string? sequence { get; set; }
        public string? systemversion { get; set; }
        public string? linkid { get; set; }
        public string? sort { get; set; }
        public string? key { get; set; }
        public string? range { get; set; }
        public List<Cliente>? CUVc { get; set; }
    }

    public class Cliente
    {
        public string? register { get; set; }
        public string? sequence { get; set; }
        public string? url { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? eMail { get; set; }
        public string? Mobile { get; set; }
    }
}