using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.DbStudio;

internal static class ConnectionStringResolver
{
    public static string Resolve(IConfiguration cfg, ZooSanMarinoContext? ctx = null)
    {
        // 1) Tu clave principal
        var zoo = cfg.GetConnectionString("ZooSanMarinoContext");
        if (!string.IsNullOrWhiteSpace(zoo)) return zoo!;

        // 2) Fallback a "Default"
        var @default = cfg.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(@default)) return @default!;

        // 3) Fallback al connection del DbContext si ya existe
        var fromCtx = ctx?.Database.GetDbConnection()?.ConnectionString;
        if (!string.IsNullOrWhiteSpace(fromCtx)) return fromCtx!;

        throw new InvalidOperationException("No se encontró la cadena de conexión. Define 'ConnectionStrings:ZooSanMarinoContext' o 'ConnectionStrings:Default'.");
    }
}
