using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LotePosturaBaseService : ILotePosturaBaseService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;

    public LotePosturaBaseService(ZooSanMarinoContext ctx, ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    public async Task<IEnumerable<LotePosturaBaseDto>> GetAllAsync()
    {
        var companyName = _current.ActiveCompanyName ?? "";

        var query =
            from lpb in _ctx.LotePosturaBases.AsNoTracking()
            where lpb.CompanyId == _current.CompanyId && lpb.DeletedAt == null
            join f in _ctx.Farms.AsNoTracking() on lpb.FarmId equals f.Id into farmGroup
            from farm in farmGroup.DefaultIfEmpty()
            join p in _ctx.Paises.AsNoTracking() on lpb.PaisId equals p.PaisId into paisGroup
            from pais in paisGroup.DefaultIfEmpty()
            orderby lpb.CreatedAt descending
            select new
            {
                lpb,
                FarmName  = (string?)farm.Name,
                PaisNombre = (string?)pais.PaisNombre
            };

        var items = await query.ToListAsync();

        return items.Select(x => Map(x.lpb, companyName, x.FarmName, x.PaisNombre));
    }

    public async Task<LotePosturaBaseDto?> GetByIdAsync(int id)
    {
        var companyName = _current.ActiveCompanyName ?? "";

        var query =
            from lpb in _ctx.LotePosturaBases.AsNoTracking()
            where lpb.LotePosturaBaseId == id
               && lpb.CompanyId == _current.CompanyId
               && lpb.DeletedAt == null
            join f in _ctx.Farms.AsNoTracking() on lpb.FarmId equals f.Id into farmGroup
            from farm in farmGroup.DefaultIfEmpty()
            join p in _ctx.Paises.AsNoTracking() on lpb.PaisId equals p.PaisId into paisGroup
            from pais in paisGroup.DefaultIfEmpty()
            select new
            {
                lpb,
                FarmName   = (string?)farm.Name,
                PaisNombre = (string?)pais.PaisNombre
            };

        var result = await query.FirstOrDefaultAsync();
        return result is null ? null : Map(result.lpb, companyName, result.FarmName, result.PaisNombre);
    }

    public async Task<LotePosturaBaseDto> CreateAsync(CreateLotePosturaBaseDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.LoteNombre))
            throw new InvalidOperationException("El nombre del lote es requerido.");
        if (dto.CantidadHembras < 0 || dto.CantidadMachos < 0 || dto.CantidadMixtas < 0)
            throw new InvalidOperationException("Las cantidades no pueden ser negativas.");

        var e = new LotePosturaBase
        {
            CompanyId        = _current.CompanyId,
            CreatedByUserId  = _current.UserId,
            PaisId           = _current.PaisId,
            LoteNombre       = dto.LoteNombre.Trim(),
            CodigoErp        = string.IsNullOrWhiteSpace(dto.CodigoErp) ? null : dto.CodigoErp.Trim(),
            CantidadHembras  = dto.CantidadHembras,
            CantidadMachos   = dto.CantidadMachos,
            CantidadMixtas   = dto.CantidadMixtas,
            FarmId           = dto.FarmId,
            ErpCreate        = dto.ErpCreate,
            CreatedAt        = DateTime.UtcNow
        };

        _ctx.LotePosturaBases.Add(e);
        await _ctx.SaveChangesAsync();

        // Resolver nombre de granja para devolver en la respuesta
        string? farmNombre = null;
        if (e.FarmId.HasValue)
            farmNombre = await _ctx.Farms.AsNoTracking()
                .Where(f => f.Id == e.FarmId.Value)
                .Select(f => f.Name)
                .FirstOrDefaultAsync();

        return Map(e, _current.ActiveCompanyName ?? "", farmNombre, null);
    }

    public async Task<LotePosturaBaseDto> UpdateAsync(int id, UpdateLotePosturaBaseDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.LoteNombre))
            throw new InvalidOperationException("El nombre del lote es requerido.");
        if (dto.CantidadHembras < 0 || dto.CantidadMachos < 0 || dto.CantidadMixtas < 0)
            throw new InvalidOperationException("Las cantidades no pueden ser negativas.");

        var e = await _ctx.LotePosturaBases
            .Where(x => x.LotePosturaBaseId == id
                     && x.CompanyId == _current.CompanyId
                     && x.DeletedAt == null)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Lote base {id} no encontrado.");

        e.LoteNombre      = dto.LoteNombre.Trim();
        e.CodigoErp       = string.IsNullOrWhiteSpace(dto.CodigoErp) ? null : dto.CodigoErp.Trim();
        e.CantidadHembras = dto.CantidadHembras;
        e.CantidadMachos  = dto.CantidadMachos;
        e.CantidadMixtas  = dto.CantidadMixtas;
        e.FarmId          = dto.FarmId;
        e.ErpCreate       = dto.ErpCreate;

        await _ctx.SaveChangesAsync();

        string? farmNombre = null;
        if (e.FarmId.HasValue)
            farmNombre = await _ctx.Farms.AsNoTracking()
                .Where(f => f.Id == e.FarmId.Value)
                .Select(f => f.Name)
                .FirstOrDefaultAsync();

        return Map(e, _current.ActiveCompanyName ?? "", farmNombre, null);
    }

    public async Task DeleteAsync(int id)
    {
        var e = await _ctx.LotePosturaBases
            .Where(x => x.LotePosturaBaseId == id
                     && x.CompanyId == _current.CompanyId
                     && x.DeletedAt == null)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Lote base {id} no encontrado.");

        // Soft delete
        e.DeletedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
    }

    // ----------------------------------------------------------------
    private static LotePosturaBaseDto Map(
        LotePosturaBase e,
        string?  companyNombre,
        string?  farmNombre,
        string?  paisNombre) =>
        new(
            e.LotePosturaBaseId,
            e.LoteNombre,
            e.CodigoErp,
            e.CantidadHembras,
            e.CantidadMachos,
            e.CantidadMixtas,
            e.CompanyId,
            companyNombre,
            e.CreatedByUserId,
            e.PaisId,
            paisNombre,
            e.FarmId,
            farmNombre,
            e.ErpCreate,
            e.CreatedAt
        );
}
