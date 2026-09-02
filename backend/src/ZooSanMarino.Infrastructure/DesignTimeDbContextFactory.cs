// Ubícalo en: ZooSanMarino.Infrastructure/DesignTimeDbContextFactory.cs

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ZooSanMarinoContext>
    {
        public ZooSanMarinoContext CreateDbContext(string[] args)
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../ZooSanMarino.API");

            var config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.Development.json", optional: false)
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<ZooSanMarinoContext>();
            optionsBuilder
                .UseNpgsql(config.GetConnectionString("ZooSanMarinoContext"))
                .UseSnakeCaseNamingConvention();
            // Antes lo hacía OnConfiguring; se movió acá porque el pool del API lo prohíbe.
            ZooSanMarinoContext.ConfigurarWarnings(optionsBuilder);

            return new ZooSanMarinoContext(optionsBuilder.Options);
        }
    }
}
