using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Módulos de permisos (ancla del partial). Solo resuelve datos y materializa: las reglas viven en
/// <see cref="PermisoModuloCalculos"/>.
/// <list type="bullet">
///   <item><c>Funciones/PermissionModuleService.Catalogo.cs</c> — CRUD de módulos y clasificación.</item>
///   <item><c>Funciones/PermissionModuleService.AsignacionEmpresa.cs</c> — módulos de cada empresa.</item>
/// </list>
/// Plan: <c>fase_de_desarrollo/modulos_permisos_por_empresa_plan.md</c>.
/// </summary>
public partial class PermissionModuleService : IPermissionModuleService
{
    private readonly ZooSanMarinoContext _ctx;

    public PermissionModuleService(ZooSanMarinoContext ctx) => _ctx = ctx;

    private sealed record PermisoCatalogo(int Id, string Key);

    private async Task<List<PermisoCatalogo>> CatalogoAsync()
    {
        var filas = await _ctx.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Key)
            .Select(p => new { p.Id, p.Key })
            .ToListAsync();
        return filas.Select(p => new PermisoCatalogo(p.Id, p.Key)).ToList();
    }

    /// <summary>Clasificación vigente en la BD: key de permiso → keys de módulo.</summary>
    private async Task<Dictionary<string, IReadOnlyCollection<string>>> ClasificacionAsync()
    {
        var filas = await _ctx.PermissionModulePermissions
            .AsNoTracking()
            .Select(x => new { PermisoKey = x.Permission.Key, ModuloKey = x.Module.Key })
            .ToListAsync();

        return filas
            .GroupBy(x => x.PermisoKey, PermisoModuloCalculos.Comparador)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyCollection<string>)g.Select(x => x.ModuloKey).ToList(),
                PermisoModuloCalculos.Comparador);
    }

    /// <summary>
    /// Módulos prendidos por empresa, SOLO de las empresas con configuración de módulos (alguna fila en
    /// <c>company_permission_modules</c>). Una empresa sin filas no se toca: su config es manual.
    /// </summary>
    private async Task<Dictionary<int, List<string>>> ModulosPorEmpresaAsync()
    {
        var filas = await _ctx.CompanyPermissionModules
            .AsNoTracking()
            .Select(x => new { x.CompanyId, x.IsEnabled, x.Module.Key })
            .ToListAsync();

        return filas
            .GroupBy(x => x.CompanyId)
            .ToDictionary(g => g.Key, g => g.Where(x => x.IsEnabled).Select(x => x.Key).ToList());
    }

    /// <summary>
    /// Aplica <see cref="PermisoModuloCalculos.ResolverHabilitados"/> sobre <c>company_permissions</c> de
    /// una empresa. No guarda: el llamador hace un único <c>SaveChangesAsync</c> (atómico).
    /// Lo apagado se conserva como fila <c>false</c>, igual que la pantalla de permisos por empresa.
    /// </summary>
    private async Task<(int Prendidos, int Apagados)> MaterializarAsync(
        int companyId,
        IReadOnlyList<PermisoCatalogo> catalogo,
        EstadoModulosEmpresa antes,
        EstadoModulosEmpresa despues)
    {
        var filas = await _ctx.CompanyPermissions
            .Where(cp => cp.CompanyId == companyId)
            .ToListAsync();
        var filaPorPermiso = filas.ToDictionary(f => f.PermissionId);
        var keyPorId = catalogo.ToDictionary(p => p.Id, p => p.Key);

        var habilitadosAntes = filas
            .Where(f => f.IsEnabled && keyPorId.ContainsKey(f.PermissionId))
            .Select(f => keyPorId[f.PermissionId]);

        var habilitadosDespues = new HashSet<string>(
            PermisoModuloCalculos.ResolverHabilitados(catalogo.Select(p => p.Key), antes, despues, habilitadosAntes),
            PermisoModuloCalculos.Comparador);

        int prendidos = 0, apagados = 0;
        foreach (var permiso in catalogo)
        {
            var habilitado = habilitadosDespues.Contains(permiso.Key);

            if (filaPorPermiso.TryGetValue(permiso.Id, out var fila))
            {
                if (fila.IsEnabled == habilitado) continue;
                fila.IsEnabled = habilitado;
                if (habilitado) prendidos++; else apagados++;
            }
            else if (habilitado)
            {
                _ctx.CompanyPermissions.Add(new CompanyPermission
                {
                    CompanyId = companyId,
                    PermissionId = permiso.Id,
                    IsEnabled = true
                });
                prendidos++;
            }
        }

        return (prendidos, apagados);
    }
}
