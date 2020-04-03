using Microsoft.EntityFrameworkCore;

namespace Uchu.Core.Providers
{
    public class PostgresContext : UchuContextBase
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var configuration = Config.DatabaseConfiguration;

            optionsBuilder.UseNpgsql(
                $"Host={configuration.Host};" +
                $"Database={configuration.Database};" +
                $"Username={configuration.Username};" +
                $"Password={configuration.Password}"
            );
        }
    }
}