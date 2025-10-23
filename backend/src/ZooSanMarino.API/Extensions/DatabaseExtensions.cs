// file: backend/src/ZooSanMarino.API/Extensions/DatabaseExtensions.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Infrastructure.Persistence;
using ZooSanMarino.Infrastructure.Seed; // PermissionSeed, MenuSeed

namespace ZooSanMarino.API.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    /// Lee Database:RunMigrations y Database:RunSeed de la configuración y ejecuta en base a ellos.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        var runMigrations = app.Configuration.GetValue<bool>("Database:RunMigrations");
        var runSeed       = app.Configuration.GetValue<bool>("Database:RunSeed");
        await app.MigrateAndSeedAsync(runMigrations, runSeed);
    }

    /// <summary>
    /// Ejecuta migraciones y/o seed según flags.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app, bool runMigrations, bool runSeed)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var logger = sp.GetRequiredService<ILoggerFactory>()
                       .CreateLogger("Startup:DB");

        try
        {
            var ctx = sp.GetRequiredService<ZooSanMarinoContext>(); // ← corregido

            if (runMigrations)
            {
                await ctx.Database.MigrateAsync();
                logger.LogInformation("✅ EF Core migrations applied.");
            }

            if (runSeed)
            {
                await PermissionSeed.EnsureAsync(ctx);
                await MenuSeed.EnsureAsync(ctx);
                // await CatalogItemSeed.EnsureAsync(ctx);
                logger.LogInformation("✅ Seed completed.");
            }

            if (!runMigrations && !runSeed)
            {
                logger.LogInformation("ℹ️ Database:RunMigrations/RunSeed = false; no actions executed.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error migrating/seeding database");
            throw;
        }
    }
}
