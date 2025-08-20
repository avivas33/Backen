using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Api_Celero.Models;

namespace Api_Celero
{
    public static class SqliteDbConfig
    {
        public static void AddSqliteDb(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<ReciboCajaOfflineContext>(options =>
                options.UseSqlite(connectionString));
        }
    }
}
