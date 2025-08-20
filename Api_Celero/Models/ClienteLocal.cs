using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Api_Celero.Models
{
    public class ClienteLocal
    {
        [Key]
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string VATNr { get; set; } = string.Empty;
        public string eMail { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Closed { get; set; } = string.Empty;
        public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
    }

    public class ClienteApiResponse
    {
        public ClienteApiData data { get; set; } = new ClienteApiData();
    }

    public class ClienteApiData
    {
        public string register { get; set; } = string.Empty;
        public string sequence { get; set; } = string.Empty;
        public string systemversion { get; set; } = string.Empty;
        public string linkid { get; set; } = string.Empty;
        public List<ClienteApi> CUVc { get; set; } = new List<ClienteApi>();
    }

    public class ClienteApi
    {
        public string register { get; set; } = string.Empty;
        public string sequence { get; set; } = string.Empty;
        public string url { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string VATNr { get; set; } = string.Empty;
        public string eMail { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Closed { get; set; } = string.Empty;
    }
}
