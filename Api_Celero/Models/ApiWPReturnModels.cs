
namespace Api_Celero.Models
{
    /// <summary>
    /// Modelo para la respuesta de la API ApiWPReturnVc
    /// </summary>
    public class ApiWPReturnResponse
    {
        public ApiWPReturnData? data { get; set; }
    }

    /// <summary>
    /// Datos de la respuesta ApiWPReturnVc
    /// </summary>
    public class ApiWPReturnData
    {
        public string register { get; set; } = string.Empty;
        public string sequence { get; set; } = string.Empty;
        public string systemversion { get; set; } = string.Empty;
        public string linkid { get; set; } = string.Empty;
        public string sort { get; set; } = string.Empty;
        public string key { get; set; } = string.Empty;
        public string range { get; set; } = string.Empty;
        public List<ApiWPReturnItem>? ApiWPReturnVc { get; set; }
    }

    /// <summary>
    /// Item individual de la respuesta ApiWPReturnVc
    /// </summary>
    public class ApiWPReturnItem
    {
        public string register { get; set; } = string.Empty;
        public string sequence { get; set; } = string.Empty;
        public string url { get; set; } = string.Empty;
        public string IVSerNr { get; set; } = string.Empty;
        public string cufe { get; set; } = string.Empty;
    }
}
