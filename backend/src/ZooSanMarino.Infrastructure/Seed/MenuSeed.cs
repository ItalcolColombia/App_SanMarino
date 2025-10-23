using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Seed;

public static class MenuSeed
{
    public static async Task EnsureAsync(ZooSanMarinoContext ctx)
    {
        if (await ctx.Menus.AnyAsync()) return;

        // Raíces
        var dashboard = new Menu { Label = "Dashboard", Icon = "faHome", Route = "/dashboard", Order = 1, IsActive = true };
        var admin     = new Menu { Label = "Administración", Icon = "faTools", Order = 2, IsActive = true };

        ctx.Menus.AddRange(dashboard, admin);
        await ctx.SaveChangesAsync();

        // Hijos de admin
        var usuarios = new Menu { Label = "Usuarios",  Icon = "faUsers",      Route = "/config/users",      ParentId = admin.Id, Order = 1, IsActive = true };
        var roles    = new Menu { Label = "Roles",     Icon = "faUserShield", Route = "/config/roles",      ParentId = admin.Id, Order = 2, IsActive = true };
        var permisos = new Menu { Label = "Permisos",  Icon = "faKey",        Route = "/config/permissions",ParentId = admin.Id, Order = 3, IsActive = true };
        var menus    = new Menu { Label = "Menús",     Icon = "faBars",       Route = "/config/menus",      ParentId = admin.Id, Order = 4, IsActive = true };

        ctx.Menus.AddRange(usuarios, roles, permisos, menus);
        await ctx.SaveChangesAsync();

        // Vincular permisos por KEY (idempotente)
        var permByKey = await ctx.Permissions.ToDictionaryAsync(p => p.Key, p => p.Id);

        var links = new List<MenuPermission>();
        if (permByKey.TryGetValue("view_reports", out var viewReports))    links.Add(new MenuPermission { MenuId = dashboard.Id, PermissionId = viewReports });
        if (permByKey.TryGetValue("manage_users", out var manageUsers))    links.Add(new MenuPermission { MenuId = usuarios.Id,  PermissionId = manageUsers });
        if (permByKey.TryGetValue("manage_roles", out var manageRoles))    links.Add(new MenuPermission { MenuId = roles.Id,     PermissionId = manageRoles });
        if (permByKey.TryGetValue("listarPermisos", out var listarPerms))  links.Add(new MenuPermission { MenuId = permisos.Id,  PermissionId = listarPerms });
        if (permByKey.TryGetValue("manage_menus", out var manageMenus))    links.Add(new MenuPermission { MenuId = menus.Id,     PermissionId = manageMenus });

        if (links.Count > 0)
        {
            await ctx.MenuPermissions.AddRangeAsync(links);
            await ctx.SaveChangesAsync();
        }
    }
}
