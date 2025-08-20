using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Api_Celero.Models
{
    public class ReciboCajaOffline
    {
        [Key]
        public int Id { get; set; }
        public string SerNr { get; set; }
        public DateTime TransDate { get; set; }
        public string PayMode { get; set; }
        public string Person { get; set; }
        public string CUCode { get; set; }
        public string RefStr { get; set; }
        public string DetallesJson { get; set; } // Serializado como JSON
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
        public bool Pendiente { get; set; } = true;
    }

    public class ReciboCajaOfflineContext : DbContext
    {
        public DbSet<ReciboCajaOffline> RecibosOffline { get; set; }
        public DbSet<ClienteLocal> ClientesLocales { get; set; }
        public DbSet<PagoACHFoto> PagoACHFotos { get; set; }
        public DbSet<CodigoVerificacion> CodigosVerificacion { get; set; }
        public ReciboCajaOfflineContext(DbContextOptions<ReciboCajaOfflineContext> options) : base(options) { }
    }
}
