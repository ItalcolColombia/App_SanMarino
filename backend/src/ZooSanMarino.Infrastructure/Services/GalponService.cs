// file: src/ZooSanMarino.Infrastructure/Services/GalponService.cs

using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using ZooSanMarino.Application.DTOs;
using AppInterfaces = ZooSanMarino.Application.Interfaces;
using CommonDtos = ZooSanMarino.Application.DTOs.Common;
using GalponDtos = ZooSanMarino.Application.DTOs.Galpones;
using SharedDtos = ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using ZooSanMarino.Application.DTOs.Farms;

namespace ZooSanMarino.Infrastructure.Services;

public class GalponService : AppInterfaces.IGalponService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly AppInterfaces.ICurrentUser _current;

    public GalponService(ZooSanMarinoContext ctx, AppInterfaces.ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // BÚSQUEDA DETALLADA (PAGINADA)
    // ─────────────────────────────────────────────────────────────────────────────
    public async Task<CommonDtos.PagedResult<GalponDtos.GalponDetailDto>> SearchAsync(GalponDtos.GalponSearchRequest req)
    {
        var q = _ctx.Galpones
            .AsNoTracking()
            .Where(g => g.CompanyId == _current.CompanyId);

        if (req.SoloActivos) q = q.Where(g => g.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var term = req.Search.Trim().ToLower();
            q = q.Where(g =>
                g.GalponId.ToLower().Contains(term) ||
                g.GalponNombre.ToLower().Contains(term));
            // Para PostgreSQL puedes usar ILIKE:
            // q = q.Where(g => EF.Functions.ILike(g.GalponId, $"%{req.Search}%") ||
            //                  EF.Functions.ILike(g.GalponNombre, $"%{req.Search}%"));
        }

        if (req.GranjaId.HasValue)            q = q.Where(g => g.GranjaId == req.GranjaId);
        if (!string.IsNullOrWhiteSpace(req.NucleoId))  q = q.Where(g => g.NucleoId == req.NucleoId);
        if (!string.IsNullOrWhiteSpace(req.TipoGalpon)) q = q.Where(g => g.TipoGalpon == req.TipoGalpon);

        q = ApplyOrder(q, req.SortBy, req.SortDesc);

        var total = await q.LongCountAsync();
        var items = await ProjectToDetail(q)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .ToListAsync();

        return new CommonDtos.PagedResult<GalponDtos.GalponDetailDto>
        {
            Page     = req.Page,
            PageSize = req.PageSize,
            Total    = total,
            Items    = items
        };
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // QUERIES DETALLADAS (NO PAGINADAS)
    // ─────────────────────────────────────────────────────────────────────────────
    public async Task<GalponDtos.GalponDetailDto?> GetDetailByIdAsync(string galponId)
    {
        var q = _ctx.Galpones.AsNoTracking()
            .Where(g => g.CompanyId == _current.CompanyId &&
                        g.GalponId   == galponId &&
                        g.DeletedAt  == null);
        return await ProjectToDetail(q).SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<GalponDtos.GalponDetailDto>> GetAllDetailAsync()
    {
        var q = _ctx.Galpones.AsNoTracking()
            .Where(g => g.CompanyId == _current.CompanyId && g.DeletedAt == null);
        return await ProjectToDetail(q).ToListAsync();
    }

    public async Task<GalponDtos.GalponDetailDto?> GetDetailByIdSimpleAsync(string galponId)
    {
        var q = _ctx.Galpones.AsNoTracking()
            .Where(g => g.CompanyId == _current.CompanyId &&
                        g.GalponId   == galponId &&
                        g.DeletedAt  == null);
        return await ProjectToDetail(q).SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<GalponDtos.GalponDetailDto>> GetDetailByGranjaAndNucleoAsync(int granjaId, string nucleoId)
    {
        var q = _ctx.Galpones.AsNoTracking()
            .Where(g => g.CompanyId == _current.CompanyId &&
                        g.DeletedAt  == null &&
                        g.GranjaId   == granjaId &&
                        g.NucleoId   == nucleoId);
        return await ProjectToDetail(q).ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // CRUD / LISTADOS QUE USA EL CONTROLLER (DETALLE CONSISTENTE)
    // ─────────────────────────────────────────────────────────────────────────────
    public async Task<IEnumerable<GalponDtos.GalponDetailDto>> GetAllAsync()
    {
        var q = _ctx.Galpones.AsNoTracking()
            .Where(g => g.CompanyId == _current.CompanyId && g.DeletedAt == null);
        return await ProjectToDetail(q).ToListAsync();
    }

    public async Task<GalponDtos.GalponDetailDto?> GetByIdAsync(string galponId)
    {
        var q = _ctx.Galpones.AsNoTracking()
            .Where(g => g.CompanyId == _current.CompanyId &&
                        g.GalponId   == galponId &&
                        g.DeletedAt  == null);
        return await ProjectToDetail(q).SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<GalponDtos.GalponDetailDto>> GetByGranjaAndNucleoAsync(int granjaId, string nucleoId)
    {
        var q = _ctx.Galpones.AsNoTracking()
            .Where(g => g.CompanyId == _current.CompanyId &&
                        g.DeletedAt  == null &&
                        g.GranjaId   == granjaId &&
                        g.NucleoId   == nucleoId);
        return await ProjectToDetail(q).ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // CREATE / UPDATE / DELETE
    // ─────────────────────────────────────────────────────────────────────────────
    public async Task<GalponDtos.GalponDetailDto> CreateAsync(CreateGalponDto dto)
    {
        await EnsureFarmExists(dto.GranjaId);
        await EnsureNucleoExists(dto.NucleoId, dto.GranjaId);

        var exists = await _ctx.Galpones.AnyAsync(x =>
            x.CompanyId == _current.CompanyId &&
            x.GalponId  == dto.GalponId);

        if (exists) throw new InvalidOperationException("Ya existe un galpón con ese Id.");

        var ent = new Galpon
        {
            GalponId        = dto.GalponId,
            GalponNombre    = dto.GalponNombre,
            NucleoId        = dto.NucleoId,
            GranjaId        = dto.GranjaId,
            Ancho           = dto.Ancho,
            Largo           = dto.Largo,
            TipoGalpon      = dto.TipoGalpon,
            CompanyId       = _current.CompanyId,
            CreatedByUserId = _current.UserId,
            CreatedAt       = DateTime.UtcNow
        };

        _ctx.Galpones.Add(ent);
        await _ctx.SaveChangesAsync();

        // Releer con proyección a detalle (para traer Farm/Nucleo/Company)
        return await GetDetailByIdAsync(ent.GalponId)
               ?? new GalponDtos.GalponDetailDto(
                    ent.GalponId, ent.GalponNombre, ent.NucleoId, ent.GranjaId,
                    ent.Ancho, ent.Largo, ent.TipoGalpon, ent.CompanyId,
                    ent.CreatedByUserId, ent.CreatedAt, ent.UpdatedByUserId, ent.UpdatedAt,
                    new FarmLiteDto(ent.Farm?.Id ?? dto.GranjaId, ent.Farm?.Name ?? "", ent.Farm?.RegionalId ?? 0, ent.Farm?.MunicipioId ?? 0,ent.Farm?.DepartamentoId ?? 0),
                    new SharedDtos.NucleoLiteDto(ent.NucleoId, "", ent.GranjaId),
                    new SharedDtos.CompanyLiteDto(
                        ent.CompanyId,
                        ent.Company?.Name ?? "",
                        ent.Company?.VisualPermissions ?? Array.Empty<string>(),
                        ent.Company?.MobileAccess ?? false,
                        ent.Company?.Identifier)
                    );
    }

    public async Task<GalponDtos.GalponDetailDto?> UpdateAsync(UpdateGalponDto dto)
    {
        var ent = await _ctx.Galpones.SingleOrDefaultAsync(x =>
            x.CompanyId == _current.CompanyId &&
            x.GalponId  == dto.GalponId);

        if (ent is null || ent.DeletedAt != null) return null;

        await EnsureFarmExists(dto.GranjaId);
        await EnsureNucleoExists(dto.NucleoId, dto.GranjaId);

        ent.GalponNombre   = dto.GalponNombre;
        ent.NucleoId       = dto.NucleoId;
        ent.GranjaId       = dto.GranjaId;
        ent.Ancho          = dto.Ancho;
        ent.Largo          = dto.Largo;
        ent.TipoGalpon     = dto.TipoGalpon;
        ent.UpdatedByUserId= _current.UserId;
        ent.UpdatedAt      = DateTime.UtcNow;

        await _ctx.SaveChangesAsync();

        return await GetDetailByIdAsync(ent.GalponId);
    }

    public async Task<bool> DeleteAsync(string galponId)
    {
        var ent = await _ctx.Galpones.SingleOrDefaultAsync(x =>
            x.CompanyId == _current.CompanyId &&
            x.GalponId  == galponId);

        if (ent is null || ent.DeletedAt != null) return false;

        ent.DeletedAt       = DateTime.UtcNow;
        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt       = DateTime.UtcNow;

        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HardDeleteAsync(string galponId)
    {
        var ent = await _ctx.Galpones.SingleOrDefaultAsync(x =>
            x.CompanyId == _current.CompanyId &&
            x.GalponId  == galponId);
        if (ent is null) return false;

        _ctx.Galpones.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // HELPERS PRIVADOS
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task EnsureFarmExists(int granjaId)
    {
        var exists = await _ctx.Farms.AsNoTracking()
            .AnyAsync(f => f.Id == granjaId && f.CompanyId == _current.CompanyId);
        if (!exists) throw new InvalidOperationException("Granja no existe o no pertenece a la compañía.");
    }

    private async Task EnsureNucleoExists(string nucleoId, int granjaId)
    {
        var exists = await _ctx.Nucleos.AsNoTracking()
            .AnyAsync(n => n.NucleoId == nucleoId &&
                           n.GranjaId == granjaId &&
                           n.CompanyId == _current.CompanyId);
        if (!exists) throw new InvalidOperationException("Núcleo no existe en la granja o no pertenece a la compañía.");
    }

    private static IQueryable<GalponDtos.GalponDetailDto> ProjectToDetail(IQueryable<Galpon> q)
    {
        return q.Include(g => g.Farm)
                .Include(g => g.Nucleo)
                .Include(g => g.Company)
                .Select(g => new GalponDtos.GalponDetailDto(
                    g.GalponId,
                    g.GalponNombre,
                    g.NucleoId,
                    g.GranjaId,
                    g.Ancho,
                    g.Largo,
                    g.TipoGalpon,
                    g.CompanyId,
                    g.CreatedByUserId,
                    g.CreatedAt,
                    g.UpdatedByUserId,
                    g.UpdatedAt,
                    new FarmLiteDto(g.Farm.Id, g.Farm.Name, g.Farm.RegionalId, g.Farm.DepartamentoId, g.Farm.MunicipioId),
                    new SharedDtos.NucleoLiteDto(g.Nucleo.NucleoId, g.Nucleo.NucleoNombre, g.Nucleo.GranjaId),
                    new SharedDtos.CompanyLiteDto(
                        g.CompanyId,
                        g.Company.Name,
                        g.Company.VisualPermissions ?? Array.Empty<string>(),
                        g.Company.MobileAccess,
                        g.Company.Identifier)

                ));
    }

    private static IQueryable<Galpon> ApplyOrder(IQueryable<Galpon> q, string sortBy, bool desc)
    {
        Expression<Func<Galpon, object>> key = sortBy?.ToLower() switch
        {
            "galpon_id"     => g => g.GalponId,
            "nucleo_id"     => g => g.NucleoId,
            "galpon_nombre" => g => g.GalponNombre,
            _               => g => g.GalponNombre
        };
        return desc ? q.OrderByDescending(key) : q.OrderBy(key);
    }
}
