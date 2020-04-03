using Microsoft.EntityFrameworkCore;

namespace Uchu.Core.Providers
{
    public class MySqlContext : UchuContextBase
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var configuration = Config.DatabaseConfiguration;
            
            optionsBuilder.UseMySql(
                $"Server={configuration.Host};" +
                $"Database={configuration.Database};" +
                $"Uid={configuration.Username};" +
                $"Pwd={configuration.Password}"
            );
        }
    }
}