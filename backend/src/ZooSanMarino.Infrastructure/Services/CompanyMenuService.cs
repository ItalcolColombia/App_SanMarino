using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class CompanyMenuService : ICompanyMenuService
{
    private readonly ZooSanMarinoContext _ctx;

    public CompanyMenuService(ZooSanMarinoContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<IEnumerable<CompanyMenuItemDto>> GetMenusForCompanyAsync(int companyId)
    {
        var assigned = await _ctx.CompanyMenus
            .AsNoTracking()
            .Where(cm => cm.CompanyId == companyId)
            .Select(cm => new { cm.MenuId, cm.IsEnabled, cm.SortOrder, cm.ParentMenuId })
            .ToListAsync();

        if (assigned.Count == 0)
            return Array.Empty<CompanyMenuItemDto>();

        var menuIds = assigned.Select(x => x.MenuId).ToHashSet();
        var infoByMenuId = assigned.ToDictionary(x => x.MenuId, x => (x.IsEnabled, x.SortOrder, x.ParentMenuId));

        var menus = await _ctx.Menus
            .AsNoTracking()
            .Where(m => m.IsActive && menuIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Label, m.Icon, m.Route, m.Order, m.ParentId })
            .ToListAsync();

        if (menus.Count == 0)
            return Array.Empty<CompanyMenuItemDto>();

        var include = new HashSet<int>(menuIds);
        foreach (var m in menus)
        {
            var parentId = m.ParentId;
            while (parentId.HasValue && include.Add(parentId.Value))
            {
                var parent = await _ctx.Menus
                    .AsNoTracking()
                    .Where(x => x.Id == parentId.Value)
                    .Select(x => new { x.Id, x.ParentId })
                    .FirstOrDefaultAsync();
                if (parent == null) break;
                parentId = parent.ParentId;
            }
        }

        var fullMenuList = await _ctx.Menus
            .AsNoTracking()
            .Where(m => m.IsActive && include.Contains(m.Id))
            .ToListAsync();

        var combined = fullMenuList.Select(m =>
        {
            var (isEnabled, sortOrder, parentMenuId) = infoByMenuId.TryGetValue(m.Id, out var i) ? i : (true, 0, (int?)null);
            var parentId = parentMenuId ?? m.ParentId;
            return new
            {
                m.Id,
                m.Label,
                m.Icon,
                m.Route,
                Order = sortOrder,
                ParentId = parentId,
                IsEnabled = isEnabled
            };
        }).ToList();

        return BuildCompanyMenuTree(combined);
    }

    public async Task SetCompanyMenusAsync(int companyId, SetCompanyMenusRequest request)
    {
        var existing = await _ctx.CompanyMenus
            .Where(cm => cm.CompanyId == companyId)
            .ToListAsync();
        _ctx.CompanyMenus.RemoveRange(existing);

        if (request.MenuIds?.Length > 0)
        {
            var toAdd = request.MenuIds
                .Distinct()
                .Select((menuId, index) => new CompanyMenu
                {
                    CompanyId = companyId,
                    MenuId = menuId,
                    IsEnabled = request.IsEnabled,
                    SortOrder = index,
                    ParentMenuId = null
                });
            _ctx.CompanyMenus.AddRange(toAdd);
        }

        await _ctx.SaveChangesAsync();
    }

    public async Task UpdateCompanyMenuStructureAsync(int companyId, UpdateCompanyMenuStructureRequest request)
    {
        if (request.Items == null || request.Items.Length == 0) return;

        var existing = await _ctx.CompanyMenus
            .Where(cm => cm.CompanyId == companyId)
            .ToDictionaryAsync(cm => cm.MenuId);

        foreach (var item in request.Items)
        {
            if (!existing.TryGetValue(item.MenuId, out var cm)) continue;
            cm.SortOrder = item.SortOrder;
            cm.ParentMenuId = item.ParentMenuId;
            cm.IsEnabled = item.IsEnabled;
        }

        await _ctx.SaveChangesAsync();
    }

    private static IEnumerable<CompanyMenuItemDto> BuildCompanyMenuTree(IEnumerable<dynamic> flat)
    {
        var ordered = flat.OrderBy(x => (int?)x.ParentId).ThenBy(x => (int)x.Order).ToList();
        var children = ordered.ToDictionary(n => (int)n.Id, _ => new List<CompanyMenuItemDto>());
        var nodes = ordered.ToDictionary(
            n => (int)n.Id,
            n => new CompanyMenuItemDto(
                n.Id,
                (string)n.Label,
                (string?)n.Icon,
                (string?)n.Route,
                (int)n.Order,
                (bool)n.IsEnabled,
                Array.Empty<CompanyMenuItemDto>()
            )
        );

        foreach (var n in ordered)
        {
            if (n.ParentId is int pid && nodes.ContainsKey(pid))
                children[pid].Add(nodes[n.Id]);
        }

        foreach (var kv in children)
        {
            var parentId = kv.Key;
            var arr = kv.Value.OrderBy(c => c.Order).ToArray();
            var existing = nodes[parentId];
            nodes[parentId] = existing with { Children = arr };
        }

        var roots = ordered.Where(n => n.ParentId is null).Select(n => nodes[(int)n.Id]).OrderBy(r => r.Order);
        return roots;
    }
}
