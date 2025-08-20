using System.Collections.Generic;

namespace Api_Celero.Models
{
    public class CuotaResponse
    {
        public CuotaData data { get; set; }
    }

    public class CuotaData
    {
        public List<Cuota> ARInstallVc { get; set; }
    }

    public class Cuota
    {
        public string DueDate { get; set; }
        public string BookRVal { get; set; }
    }
}
