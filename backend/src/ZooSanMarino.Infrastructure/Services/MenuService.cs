using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class MenuService : IMenuService
{
    private readonly ZooSanMarinoContext _ctx;
    public MenuService(ZooSanMarinoContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<MenuItemDto>> GetTreeAsync()
    {
        var list = await _ctx.Menus
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.ParentId)
            .ThenBy(m => m.Order)
            .Select(m => new { m.Id, m.Label, m.Icon, m.Route, m.Order, m.ParentId })
            .ToListAsync();

        return BuildTree(list);
    }

    // 👇 firma con Guid
    public async Task<IEnumerable<MenuItemDto>> GetForUserAsync(Guid userId)
    {
        // 1) Permisos del usuario (por roles) — comparar Guid con Guid
        var userPermKeys = await _ctx.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Key))
            .Distinct()
            .ToListAsync();

        // 2) Menús activos + permisos requeridos
        var all = await _ctx.Menus
            .AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => new
            {
                m.Id,
                m.Label,
                m.Icon,
                m.Route,
                m.Order,
                m.ParentId,
                RequiredKeys = m.MenuPermissions.Select(mp => mp.Permission.Key).ToArray()
            })
            .ToListAsync();

        // 3) Filtrar permitidos
        var allowed = all.Where(m => m.RequiredKeys.Length == 0 ||
                                     m.RequiredKeys.Intersect(userPermKeys).Any())
                         .ToList();

        if (allowed.Count == 0) return Array.Empty<MenuItemDto>();

        // 4) Incluir ancestros
        var allDict = all.ToDictionary(x => x.Id, x => x);
        var include = new HashSet<int>(allowed.Select(x => x.Id));
        foreach (var node in allowed)
        {
            var parentId = node.ParentId;
            while (parentId.HasValue && include.Add(parentId.Value))
            {
                parentId = allDict[parentId.Value].ParentId;
            }
        }

        // 5) Construir árbol
        var filtered = all.Where(x => include.Contains(x.Id))
                          .OrderBy(x => x.ParentId)
                          .ThenBy(x => x.Order)
                          .Select(x => new { x.Id, x.Label, x.Icon, x.Route, x.Order, x.ParentId })
                          .ToList();

        return BuildTree(filtered);
    }
    public async Task<MenuItemDto> CreateAsync(CreateMenuDto dto)
    {
        var m = new Menu
        {
            Label = dto.Label,
            Icon = dto.Icon,
            Route = dto.Route,
            ParentId = dto.ParentId,
            Order = dto.Order,
            IsActive = dto.IsActive
        };
        _ctx.Menus.Add(m);
        await _ctx.SaveChangesAsync();

        if (dto.PermissionIds?.Length > 0)
        {
            var links = dto.PermissionIds.Distinct().Select(pid => new MenuPermission { MenuId = m.Id, PermissionId = pid });
            _ctx.MenuPermissions.AddRange(links);
            await _ctx.SaveChangesAsync();
        }

        return new MenuItemDto(m.Id, m.Label, m.Icon, m.Route, m.Order, Array.Empty<MenuItemDto>());
    }

    public async Task<MenuItemDto?> UpdateAsync(UpdateMenuDto dto)
    {
        var m = await _ctx.Menus.Include(x => x.MenuPermissions).SingleOrDefaultAsync(x => x.Id == dto.Id);
        if (m is null) return null;

        m.Label = dto.Label;
        m.Icon = dto.Icon;
        m.Route = dto.Route;
        m.ParentId = dto.ParentId;
        m.Order = dto.Order;
        m.IsActive = dto.IsActive;

        // Reemplazar permisos requeridos
        _ctx.MenuPermissions.RemoveRange(m.MenuPermissions);
        if (dto.PermissionIds?.Length > 0)
        {
            var links = dto.PermissionIds.Distinct().Select(pid => new MenuPermission { MenuId = m.Id, PermissionId = pid });
            _ctx.MenuPermissions.AddRange(links);
        }

        await _ctx.SaveChangesAsync();
        return new MenuItemDto(m.Id, m.Label, m.Icon, m.Route, m.Order, Array.Empty<MenuItemDto>());
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var m = await _ctx.Menus.FindAsync(id);
        if (m is null) return false;

        // Validar que no tenga hijos
        var hasChildren = await _ctx.Menus.AnyAsync(x => x.ParentId == id);
        if (hasChildren) throw new InvalidOperationException("No se puede eliminar un menú con hijos.");

        _ctx.Menus.Remove(m);
        await _ctx.SaveChangesAsync();
        return true;
    }

    // ===== Helpers =====
    private static IEnumerable<MenuItemDto> BuildTree(IEnumerable<dynamic> flat)
    {
        // flat: { Id, Label, Icon, Route, Order, ParentId }
        var byId = flat.ToDictionary(x => (int)x.Id);
        var children = flat.ToDictionary(x => (int)x.Id, _ => new List<MenuItemDto>());

        foreach (var n in flat)
        {
            int id = n.Id;
            int? parentId = n.ParentId;
            if (parentId is not null && byId.ContainsKey(parentId.Value))
            {
                // child; defer, we build at the end
            }
        }

        // order siblings
        var ordered = flat.OrderBy(x => (int?)x.ParentId).ThenBy(x => (int)x.Order).ToList();

        // pre-create nodes so we can reference in children lists
        var nodes = ordered.ToDictionary(
            n => (int)n.Id,
            n => new MenuItemDto(n.Id, n.Label, (string?)n.Icon, (string?)n.Route, n.Order, Array.Empty<MenuItemDto>())
        );

        // fill children
        foreach (var n in ordered)
        {
            if (n.ParentId is int pid && nodes.ContainsKey(pid))
                (children[pid]).Add(nodes[n.Id]);
        }

        // finalize children arrays
        foreach (var kv in children)
        {
            var parentId = kv.Key;
            var arr = kv.Value.OrderBy(c => c.Order).ToArray();
            nodes[parentId] = nodes[parentId] with { Children = arr };
        }

        // roots = ParentId == null
        var roots = ordered.Where(n => n.ParentId is null).Select(n => nodes[(int)n.Id]).OrderBy(r => r.Order);
        return roots.ToArray();
    }
    
    // ZooSanMarino.Infrastructure/Services/MenuService.cs (añadir sobrecarga)
    public async Task<IEnumerable<MenuItemDto>> GetForUserAsync(Guid userId, int? companyId)
    {
        // 1) Roles del usuario (si hay companyId, filtrar por esa compañía)
        var roleIdsQuery = _ctx.UserRoles.AsNoTracking().Where(ur => ur.UserId == userId);
        if (companyId is int cid) roleIdsQuery = roleIdsQuery.Where(ur => ur.CompanyId == cid);

        var roleIds = await roleIdsQuery.Select(ur => ur.RoleId).Distinct().ToListAsync();

        // 2) Permisos desde roles
        var userPermKeys = await _ctx.RolePermissions
            .AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToListAsync();

        // 3) Menús activos + permisos requeridos
        var all = await _ctx.Menus
            .AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => new {
                m.Id, m.Label, m.Icon, m.Route, m.Order, m.ParentId,
                RequiredKeys = m.MenuPermissions.Select(mp => mp.Permission.Key).ToArray()
            })
            .ToListAsync();

        // 4) Filtrar
        var allowed = all.Where(m => m.RequiredKeys.Length == 0 ||
                                    m.RequiredKeys.Intersect(userPermKeys, StringComparer.OrdinalIgnoreCase).Any())
                        .ToList();
        if (allowed.Count == 0) return Array.Empty<MenuItemDto>();

        // 5) Incluir ancestros y construir árbol (reutiliza tu BuildTree)
        var allDict = all.ToDictionary(x => x.Id, x => x);
        var include = new HashSet<int>(allowed.Select(x => x.Id));
        foreach (var node in allowed)
        {
            var parentId = node.ParentId;
            while (parentId.HasValue && include.Add(parentId.Value))
                parentId = allDict[parentId.Value].ParentId;
        }

        var filtered = all.Where(x => include.Contains(x.Id))
                        .OrderBy(x => x.ParentId)
                        .ThenBy(x => x.Order)
                        .Select(x => new { x.Id, x.Label, x.Icon, x.Route, x.Order, x.ParentId })
                        .ToList();

        return BuildTree(filtered);
    }

}
