using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Seed;

public static class PermissionSeed
{
    // Ajusta esta lista con los permisos “core” de tu app
    private static readonly (string Key, string Description)[] DefaultPermissions = new[]
    {
        ("view_reports",       "Ver reportes"),
        ("download_reports",   "Descargar reportes"),
        ("manage_users",       "Administrar usuarios"),
        ("manage_companies",   "Administrar empresas"),
        ("manage_roles",       "Administrar roles"),
        ("manage_menus",       "Administrar menús"),
        // (Si los usas en tus pruebas)
        ("listarPermisos",     "Listar permisos"),
        ("crearUsuario",       "Crear usuario"),
        ("verlote",            "Ver lote")
    };

    public static async Task EnsureAsync(ZooSanMarinoContext ctx)
    {
        var existingKeys = await ctx.Permissions.Select(p => p.Key).ToListAsync();
        var toInsert = DefaultPermissions
            .Where(p => !existingKeys.Contains(p.Key))
            .Select(p => new Permission { Key = p.Key, Description = p.Description });

        if (toInsert.Any())
        {
            await ctx.Permissions.AddRangeAsync(toInsert);
            await ctx.SaveChangesAsync();
        }
    }
}
