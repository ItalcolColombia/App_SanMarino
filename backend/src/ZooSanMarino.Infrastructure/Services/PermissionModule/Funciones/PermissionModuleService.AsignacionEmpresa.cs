using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>Qué módulos tiene cada empresa, y su efecto sobre <c>company_permissions</c>.</summary>
public partial class PermissionModuleService
{
    public async Task<IReadOnlyList<CompanyPermissionModuleItemDto>> GetForCompanyAsync(int companyId)
    {
        var prendidos = await _ctx.CompanyPermissionModules
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsEnabled)
            .Select(x => x.ModuleId)
            .ToListAsync();
        var prendidosSet = prendidos.ToHashSet();

        var habilitados = await _ctx.CompanyPermissions
            .AsNoTracking()
            .Where(cp => cp.CompanyId == companyId && cp.IsEnabled)
            .Select(cp => cp.PermissionId)
            .ToListAsync();
        var habilitadosSet = habilitados.ToHashSet();

        var modulos = await _ctx.PermissionModules
            .AsNoTracking()
            .OrderBy(m => m.Orden).ThenBy(m => m.Nombre)
            .Select(m => new
            {
                m.Id,
                m.Key,
                m.Nombre,
                m.Descripcion,
                m.Orden,
                PermisoIds = m.Permisos.Select(x => x.PermissionId).ToList()
            })
            .ToListAsync();

        return modulos.Select(m => new CompanyPermissionModuleItemDto(
            m.Id,
            m.Key,
            m.Nombre,
            m.Descripcion,
            m.Orden,
            prendidosSet.Contains(m.Id),
            m.PermisoIds.Count,
            m.PermisoIds.Count(habilitadosSet.Contains)
        )).ToList();
    }

    public async Task<CambioPermisosDto?> SetForCompanyAsync(int companyId, SetCompanyPermissionModulesRequest request)
    {
        if (!await _ctx.Companies.AsNoTracking().AnyAsync(c => c.Id == companyId)) return null;

        var modulos = await _ctx.PermissionModules
            .AsNoTracking()
            .Select(m => new { m.Id, m.Key })
            .ToListAsync();
        var pedidos = (request?.ModuleIds ?? Array.Empty<int>()).ToHashSet();

        var filas = await _ctx.CompanyPermissionModules
            .Where(x => x.CompanyId == companyId)
            .ToListAsync();
        var filaPorModulo = filas.ToDictionary(f => f.ModuleId);

        var keyPorModulo = modulos.ToDictionary(m => m.Id, m => m.Key);
        var modulosAntes = filas
            .Where(f => f.IsEnabled && keyPorModulo.ContainsKey(f.ModuleId))
            .Select(f => keyPorModulo[f.ModuleId])
            .ToList();

        // Una fila por módulo, prendida o apagada: la configuración queda explícita.
        foreach (var modulo in modulos)
        {
            var prendido = pedidos.Contains(modulo.Id);
            if (filaPorModulo.TryGetValue(modulo.Id, out var fila)) fila.IsEnabled = prendido;
            else _ctx.CompanyPermissionModules.Add(new CompanyPermissionModule
            {
                CompanyId = companyId,
                ModuleId = modulo.Id,
                IsEnabled = prendido
            });
        }

        var modulosDespues = modulos.Where(m => pedidos.Contains(m.Id)).Select(m => m.Key).ToList();
        var clasificacion = await ClasificacionAsync();
        var (prendidos, apagados) = await MaterializarAsync(companyId, await CatalogoAsync(),
            new EstadoModulosEmpresa(clasificacion, modulosAntes),
            new EstadoModulosEmpresa(clasificacion, modulosDespues));

        await _ctx.SaveChangesAsync();
        return new CambioPermisosDto(prendidos + apagados > 0 ? 1 : 0, prendidos, apagados);
    }
}
