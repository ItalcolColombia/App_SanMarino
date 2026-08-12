// src/ZooSanMarino.Infrastructure/Services/Silos/SiloCatalogoService.cs
// Namespace PLANO aunque el archivo esté en subcarpeta (convención de CLAUDE.md): DI y referencias
// no cambian por mover el archivo.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Lista maestra de silos de la empresa activa. Scoping por <c>_current.CompanyId</c>: una empresa
/// nunca ve ni toca el catálogo de otra.
/// </summary>
public class SiloCatalogoService : ISiloCatalogoService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;

    public SiloCatalogoService(ZooSanMarinoContext ctx, ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    public async Task<IEnumerable<SiloCatalogoDto>> GetAllAsync(bool soloActivos = false, CancellationToken ct = default)
    {
        var companyId = _current.CompanyId;

        var q = _ctx.SiloCatalogo.AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.DeletedAt == null);

        if (soloActivos) q = q.Where(s => s.Activo);

        // El conteo de granjas se resuelve en la BD (no trayendo farm_silos a memoria): con 100 silos
        // y varias granjas, contar en C# implicaría cargar toda la tabla en cada listado.
        var filas = await q
            .OrderBy(s => s.Numero)
            .Select(s => new
            {
                s.Id, s.CompanyId, s.Numero, s.Nombre, s.Descripcion, s.Activo,
                Granjas = _ctx.FarmSilos.Count(fs => fs.SiloCatalogoId == s.Id && fs.DeletedAt == null)
            })
            .ToListAsync(ct);

        return filas.Select(x => new SiloCatalogoDto(
            x.Id, x.CompanyId, x.Numero, x.Nombre, x.Descripcion, x.Activo, x.Granjas));
    }

    public async Task<SiloCatalogoDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var companyId = _current.CompanyId;

        var x = await _ctx.SiloCatalogo.AsNoTracking()
            .Where(s => s.Id == id && s.CompanyId == companyId && s.DeletedAt == null)
            .Select(s => new
            {
                s.Id, s.CompanyId, s.Numero, s.Nombre, s.Descripcion, s.Activo,
                Granjas = _ctx.FarmSilos.Count(fs => fs.SiloCatalogoId == s.Id && fs.DeletedAt == null)
            })
            .FirstOrDefaultAsync(ct);

        return x is null
            ? null
            : new SiloCatalogoDto(x.Id, x.CompanyId, x.Numero, x.Nombre, x.Descripcion, x.Activo, x.Granjas);
    }

    public async Task<SiloCatalogoDto> CreateAsync(CreateSiloCatalogoDto dto, CancellationToken ct = default)
    {
        var companyId = _current.CompanyId;

        var errNumero = SiloCalculos.ValidarNumero(dto.Numero);
        if (errNumero is not null) throw new InvalidOperationException(errNumero);

        var nombre = string.IsNullOrWhiteSpace(dto.Nombre)
            ? SiloCalculos.NombreDeCatalogo(dto.Numero)
            : dto.Nombre!.Trim();

        var yaNumero = await _ctx.SiloCatalogo
            .AnyAsync(s => s.CompanyId == companyId && s.Numero == dto.Numero && s.DeletedAt == null, ct);
        if (yaNumero)
            throw new InvalidOperationException($"Ya existe un silo con el número {dto.Numero} en la lista maestra.");

        var yaNombre = await _ctx.SiloCatalogo
            .AnyAsync(s => s.CompanyId == companyId && s.Nombre == nombre && s.DeletedAt == null, ct);
        if (yaNombre)
            throw new InvalidOperationException($"Ya existe un silo llamado '{nombre}' en la lista maestra.");

        var entity = new SiloCatalogo
        {
            CompanyId = companyId,
            Numero = dto.Numero,
            Nombre = nombre,
            Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion!.Trim(),
            Activo = dto.Activo,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.SiloCatalogo.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return new SiloCatalogoDto(entity.Id, entity.CompanyId, entity.Numero, entity.Nombre,
            entity.Descripcion, entity.Activo, 0);
    }

    public async Task<SiloCatalogoDto?> UpdateAsync(int id, UpdateSiloCatalogoDto dto, CancellationToken ct = default)
    {
        var companyId = _current.CompanyId;

        var entity = await _ctx.SiloCatalogo
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId && s.DeletedAt == null, ct);
        if (entity is null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Nombre))
        {
            var nombre = dto.Nombre!.Trim();
            var choca = await _ctx.SiloCatalogo
                .AnyAsync(s => s.CompanyId == companyId && s.Nombre == nombre && s.Id != id && s.DeletedAt == null, ct);
            if (choca)
                throw new InvalidOperationException($"Ya existe un silo llamado '{nombre}' en la lista maestra.");
            entity.Nombre = nombre;
        }

        // `null` = el cliente no lo mandó ⇒ se conserva (mismo criterio que los flags de empresa).
        if (dto.Descripcion is not null)
            entity.Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        if (dto.Activo.HasValue)
            entity.Activo = dto.Activo.Value;

        entity.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var companyId = _current.CompanyId;

        var entity = await _ctx.SiloCatalogo
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId && s.DeletedAt == null, ct);
        if (entity is null) return false;

        // Un silo del catálogo con granjas colgando no se borra: la granja quedaría con una fila
        // apuntando a una entrada muerta y el nombre dejaría de ser trazable.
        var asignado = await _ctx.FarmSilos
            .CountAsync(fs => fs.SiloCatalogoId == id && fs.DeletedAt == null, ct);
        if (asignado > 0)
            throw new InvalidOperationException(
                $"No se puede eliminar '{entity.Nombre}': está asignado a {asignado} granja(s). Quítelo de las granjas primero o márquelo como inactivo.");

        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    public async Task<GenerarRangoSilosResultDto> GenerarRangoAsync(GenerarRangoSilosDto dto, CancellationToken ct = default)
    {
        var companyId = _current.CompanyId;

        var existentes = await _ctx.SiloCatalogo.AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.DeletedAt == null)
            .Select(s => s.Numero)
            .ToListAsync(ct);

        var nuevos = SiloCalculos.ExpandirRango(dto.Desde, dto.Hasta, existentes, out var error);
        if (error is not null) throw new InvalidOperationException(error);

        var ahora = DateTime.UtcNow;
        var entidades = nuevos.Select(n => new SiloCatalogo
        {
            CompanyId = companyId,
            Numero = n,
            Nombre = SiloCalculos.NombreDeCatalogo(n, dto.PatronNombre),
            Activo = true,
            CreatedAt = ahora
        }).ToList();

        if (entidades.Count > 0)
        {
            _ctx.SiloCatalogo.AddRange(entidades);
            await _ctx.SaveChangesAsync(ct);
        }

        var omitidos = (dto.Hasta - dto.Desde + 1) - entidades.Count;
        var todos = await GetAllAsync(soloActivos: false, ct);
        return new GenerarRangoSilosResultDto(entidades.Count, omitidos, todos.ToList());
    }
}
