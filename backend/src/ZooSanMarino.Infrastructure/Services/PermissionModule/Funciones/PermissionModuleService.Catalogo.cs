using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>Catálogo de módulos: alta/edición/baja y qué permisos agrupa cada uno.</summary>
public partial class PermissionModuleService
{
    public async Task<IReadOnlyList<PermissionModuleDto>> GetAllAsync()
    {
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
                Keys = m.Permisos.Select(x => x.Permission.Key).OrderBy(k => k).ToList(),
                Empresas = m.Empresas.Count(e => e.IsEnabled)
            })
            .ToListAsync();

        return modulos
            .Select(m => new PermissionModuleDto(m.Id, m.Key, m.Nombre, m.Descripcion, m.Orden, m.Keys, m.Empresas))
            .ToList();
    }

    public async Task<PermissionModuleDto?> CreateAsync(CreatePermissionModuleDto dto)
    {
        var key = (dto.Key ?? string.Empty).Trim().ToLowerInvariant();
        if (await _ctx.PermissionModules.AnyAsync(m => m.Key == key)) return null;

        var modulo = new PermissionModule
        {
            Key = key,
            Nombre = dto.Nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim(),
            Orden = dto.Orden
        };
        _ctx.PermissionModules.Add(modulo);
        await _ctx.SaveChangesAsync();

        // Nace sin permisos y sin empresas: no cambia el acceso de nadie hasta que se clasifique y asigne.
        return new PermissionModuleDto(modulo.Id, modulo.Key, modulo.Nombre, modulo.Descripcion, modulo.Orden,
            Array.Empty<string>(), 0);
    }

    public async Task<PermissionModuleDto?> UpdateAsync(int id, UpdatePermissionModuleDto dto)
    {
        var modulo = await _ctx.PermissionModules.FirstOrDefaultAsync(m => m.Id == id);
        if (modulo is null) return null;

        modulo.Nombre = dto.Nombre.Trim();
        modulo.Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        modulo.Orden = dto.Orden;
        await _ctx.SaveChangesAsync();

        return (await GetAllAsync()).FirstOrDefault(m => m.Id == id);
    }

    public async Task<EliminarModuloResultado> DeleteAsync(int id)
    {
        var modulo = await _ctx.PermissionModules.FirstOrDefaultAsync(m => m.Id == id);
        if (modulo is null) return EliminarModuloResultado.NoExiste;

        // Borrar un módulo prendido obligaría a rematerializar empresas en silencio: primero se apaga.
        if (await _ctx.CompanyPermissionModules.AnyAsync(x => x.ModuleId == id && x.IsEnabled))
            return EliminarModuloResultado.EnUso;

        _ctx.PermissionModules.Remove(modulo); // cascade: clasificación y filas apagadas de empresas
        await _ctx.SaveChangesAsync();
        return EliminarModuloResultado.Eliminado;
    }

    public async Task<CambioPermisosDto?> SetPermissionsAsync(int moduleId, SetPermissionModulePermissionsRequest request)
    {
        var modulo = await _ctx.PermissionModules.AsNoTracking().FirstOrDefaultAsync(m => m.Id == moduleId);
        if (modulo is null) return null;

        var catalogo = await CatalogoAsync();
        var idsCatalogo = catalogo.Select(p => p.Id).ToHashSet();
        var pedidos = (request?.PermissionIds ?? Array.Empty<int>())
            .Where(idsCatalogo.Contains) // un id inventado sería una FK rota
            .ToHashSet();

        var clasifAntes = await ClasificacionAsync();
        var clasifDespues = ReclasificarModulo(clasifAntes, modulo.Key,
            catalogo.Where(p => pedidos.Contains(p.Id)).Select(p => p.Key));

        var actuales = await _ctx.PermissionModulePermissions
            .Where(x => x.ModuleId == moduleId)
            .ToListAsync();
        _ctx.PermissionModulePermissions.RemoveRange(actuales.Where(x => !pedidos.Contains(x.PermissionId)));
        var yaClasificados = actuales.Select(x => x.PermissionId).ToHashSet();
        _ctx.PermissionModulePermissions.AddRange(pedidos
            .Where(id => !yaClasificados.Contains(id))
            .Select(id => new PermissionModulePermission { ModuleId = moduleId, PermissionId = id }));

        // Cambiar la clasificación mueve permisos en TODAS las empresas con configuración de módulos.
        int afectadas = 0, prendidos = 0, apagados = 0;
        foreach (var (companyId, modulosEmpresa) in await ModulosPorEmpresaAsync())
        {
            var (p, a) = await MaterializarAsync(companyId, catalogo,
                new EstadoModulosEmpresa(clasifAntes, modulosEmpresa),
                new EstadoModulosEmpresa(clasifDespues, modulosEmpresa));
            if (p + a > 0) afectadas++;
            prendidos += p;
            apagados += a;
        }

        await _ctx.SaveChangesAsync();
        return new CambioPermisosDto(afectadas, prendidos, apagados);
    }

    /// <summary>Clasificación con el módulo <paramref name="moduloKey"/> conteniendo exactamente <paramref name="keys"/>.</summary>
    private static Dictionary<string, IReadOnlyCollection<string>> ReclasificarModulo(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> clasificacion,
        string moduloKey,
        IEnumerable<string> keys)
    {
        var resultado = new Dictionary<string, IReadOnlyCollection<string>>(PermisoModuloCalculos.Comparador);
        foreach (var (permiso, modulos) in clasificacion)
        {
            var resto = modulos.Where(m => !PermisoModuloCalculos.Comparador.Equals(m, moduloKey)).ToList();
            if (resto.Count > 0) resultado[permiso] = resto;
        }
        foreach (var permiso in keys)
        {
            var previos = resultado.TryGetValue(permiso, out var r) ? r : Array.Empty<string>();
            resultado[permiso] = previos.Append(moduloKey).ToList();
        }
        return resultado;
    }
}
