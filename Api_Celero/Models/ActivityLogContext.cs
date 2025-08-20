using Microsoft.EntityFrameworkCore;

namespace Api_Celero.Models
{
    public class ActivityLogContext : DbContext
    {
        public ActivityLogContext(DbContextOptions<ActivityLogContext> options) : base(options)
        {
        }

        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<RequestLog> RequestLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de ActivityLog
            modelBuilder.Entity<ActivityLog>(entity =>
            {
                entity.HasIndex(e => e.EventType);
                entity.HasIndex(e => e.IpAddress);
                entity.HasIndex(e => e.Fingerprint);
                entity.HasIndex(e => e.ClientId);
                entity.HasIndex(e => e.CreatedAtUtc);
                entity.HasIndex(e => new { e.EventType, e.CreatedAtUtc });
                
                entity.Property(e => e.Amount)
                    .HasPrecision(18, 2);
            });

            // Configuración de RequestLog
            modelBuilder.Entity<RequestLog>(entity =>
            {
                entity.HasIndex(e => e.IpAddress);
                entity.HasIndex(e => e.RequestTimeUtc);
                entity.HasIndex(e => e.Method);
                entity.HasIndex(e => e.StatusCode);
                entity.HasIndex(e => new { e.IpAddress, e.RequestTimeUtc });
            });
        }
    }
}