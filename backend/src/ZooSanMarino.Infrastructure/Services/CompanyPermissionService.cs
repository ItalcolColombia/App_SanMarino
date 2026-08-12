using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Configuración del eje permiso↔empresa. Solo resuelve datos: las reglas viven en
/// <c>CompanyPermissionCalculos</c>.
/// </summary>
public class CompanyPermissionService : ICompanyPermissionService
{
    private readonly ZooSanMarinoContext _ctx;

    public CompanyPermissionService(ZooSanMarinoContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<CompanyPermissionItemDto>> GetPermissionsForCompanyAsync(int companyId)
    {
        var catalogo = await _ctx.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Key)
            .Select(p => new { p.Id, p.Key, p.Description })
            .ToListAsync();

        if (catalogo.Count == 0) return Array.Empty<CompanyPermissionItemDto>();

        var habilitados = await _ctx.CompanyPermissions
            .AsNoTracking()
            .Where(cp => cp.CompanyId == companyId && cp.IsEnabled)
            .Select(cp => cp.PermissionId)
            .ToListAsync();
        var habilitadosSet = habilitados.ToHashSet();

        // Cuántos roles de la empresa usan cada permiso (la empresa se vincula al rol por
        // role_companies O por user_roles; ambos caminos existen en los datos).
        var rolesDeLaEmpresa = await RoleIdsDeEmpresasAsync(new[] { companyId });

        var enUso = await _ctx.RolePermissions
            .AsNoTracking()
            .Where(rp => rolesDeLaEmpresa.Contains(rp.RoleId))
            .GroupBy(rp => rp.PermissionId)
            .Select(g => new { PermissionId = g.Key, Roles = g.Select(x => x.RoleId).Distinct().Count() })
            .ToListAsync();
        var enUsoPorPermiso = enUso.ToDictionary(x => x.PermissionId, x => x.Roles);

        return catalogo.Select(p => new CompanyPermissionItemDto(
            p.Id,
            p.Key,
            p.Description,
            habilitadosSet.Contains(p.Id),
            enUsoPorPermiso.TryGetValue(p.Id, out var n) ? n : 0
        )).ToList();
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyCollection<string>>> GetEnabledKeysAsync(
        IEnumerable<int> companyIds)
    {
        var ids = (companyIds ?? Array.Empty<int>()).Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<int, IReadOnlyCollection<string>>();

        var filas = await _ctx.CompanyPermissions
            .AsNoTracking()
            .Where(cp => ids.Contains(cp.CompanyId) && cp.IsEnabled)
            .Select(cp => new { cp.CompanyId, cp.Permission.Key })
            .ToListAsync();

        // Una empresa SIN filas queda fuera del diccionario a propósito: el cálculo la trata como
        // "sin configurar" y aplica fail-closed.
        return filas
            .GroupBy(x => x.CompanyId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyCollection<string>)g.Select(x => x.Key).Distinct().ToList());
    }

    public async Task SetPermissionsForCompanyAsync(int companyId, SetCompanyPermissionsRequest request)
    {
        var deseados = (request?.PermissionIds ?? Array.Empty<int>()).Distinct().ToHashSet();

        // Solo ids que existan en el catálogo (un id inventado quedaría como FK rota).
        if (deseados.Count > 0)
        {
            var validos = await _ctx.Permissions
                .AsNoTracking()
                .Where(p => deseados.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();
            deseados = validos.ToHashSet();
        }

        var existentes = await _ctx.CompanyPermissions
            .Where(cp => cp.CompanyId == companyId)
            .ToListAsync();
        var existentesPorId = existentes.ToDictionary(cp => cp.PermissionId);

        foreach (var permissionId in deseados)
        {
            if (existentesPorId.TryGetValue(permissionId, out var fila)) fila.IsEnabled = true;
            else _ctx.CompanyPermissions.Add(new CompanyPermission
            {
                CompanyId = companyId,
                PermissionId = permissionId,
                IsEnabled = true
            });
        }

        // Lo desmarcado se apaga, no se borra: queda el rastro de que se decidió apagarlo.
        foreach (var fila in existentes.Where(cp => !deseados.Contains(cp.PermissionId)))
            fila.IsEnabled = false;

        await _ctx.SaveChangesAsync();
    }

    public async Task SembrarCatalogoCompletoSiVaciaAsync(int companyId)
    {
        var yaTiene = await _ctx.CompanyPermissions.AnyAsync(cp => cp.CompanyId == companyId);
        if (yaTiene) return;

        var permissionIds = await _ctx.Permissions.AsNoTracking().Select(p => p.Id).ToListAsync();
        if (permissionIds.Count == 0) return;

        _ctx.CompanyPermissions.AddRange(permissionIds.Select(id => new CompanyPermission
        {
            CompanyId = companyId,
            PermissionId = id,
            IsEnabled = true
        }));

        await _ctx.SaveChangesAsync();
    }

    /// <summary>
    /// Roles vinculados a las empresas dadas. La empresa llega al rol por <c>role_companies</c> o por
    /// <c>user_roles.company_id</c>; los dos caminos están poblados en producción, así que se unen.
    /// </summary>
    private async Task<HashSet<int>> RoleIdsDeEmpresasAsync(IEnumerable<int> companyIds)
    {
        var ids = companyIds.ToList();

        var porRoleCompanies = await _ctx.RoleCompanies
            .AsNoTracking()
            .Where(rc => ids.Contains(rc.CompanyId))
            .Select(rc => rc.RoleId)
            .ToListAsync();

        var porUserRoles = await _ctx.UserRoles
            .AsNoTracking()
            .Where(ur => ids.Contains(ur.CompanyId))
            .Select(ur => ur.RoleId)
            .ToListAsync();

        return porRoleCompanies.Concat(porUserRoles).ToHashSet();
    }
}
