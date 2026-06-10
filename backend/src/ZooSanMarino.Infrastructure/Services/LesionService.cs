using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.DTOs.Lesiones;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LesionService : ILesionService
{
    private static readonly HashSet<string> ModulosValidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "REPRODUCTORA", "APOYO", "ENGORDE"
    };

    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;

    public LesionService(ZooSanMarinoContext ctx, ICurrentUser currentUser, ICompanyResolver companyResolver)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
    }

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var cid = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName);
            if (cid.HasValue) return cid.Value;
        }
        return _currentUser.CompanyId;
    }

    public async Task<LesionDto?> GetByIdAsync(long id, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var entity = await _ctx.Lesiones
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && x.DeletedAt == null, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<PagedResult<LesionDto>> SearchAsync(LesionSearchRequest req, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        var query = _ctx.Lesiones
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId);

        if (req.SoloActivos)
            query = query.Where(x => x.DeletedAt == null && x.Status == "A");

        if (!string.IsNullOrWhiteSpace(req.ModuloOrigen))
        {
            var m = req.ModuloOrigen.ToUpper();
            query = query.Where(x => x.ModuloOrigen == m);
        }

        if (req.ClienteId.HasValue)
            query = query.Where(x => x.ClienteId == req.ClienteId.Value);

        if (req.FarmId.HasValue)
            query = query.Where(x => x.FarmId == req.FarmId.Value);

        if (!string.IsNullOrWhiteSpace(req.GalponId))
            query = query.Where(x => x.GalponId == req.GalponId);

        if (req.LoteId.HasValue)
            query = query.Where(x => x.LoteId == req.LoteId.Value);

        if (!string.IsNullOrWhiteSpace(req.LoteReproductoraId))
            query = query.Where(x => x.LoteReproductoraId == req.LoteReproductoraId);

        if (!string.IsNullOrWhiteSpace(req.TipoLesion))
        {
            var t = req.TipoLesion.ToLower();
            query = query.Where(x => x.TipoLesion.ToLower().Contains(t));
        }

        if (req.FechaDesde.HasValue)
            query = query.Where(x => x.FechaRegistro >= req.FechaDesde.Value);

        if (req.FechaHasta.HasValue)
            query = query.Where(x => x.FechaRegistro <= req.FechaHasta.Value);

        query = req.SortBy.ToLower() switch
        {
            "tipo_lesion"   => req.SortDesc ? query.OrderByDescending(x => x.TipoLesion)    : query.OrderBy(x => x.TipoLesion),
            "modulo_origen" => req.SortDesc ? query.OrderByDescending(x => x.ModuloOrigen)  : query.OrderBy(x => x.ModuloOrigen),
            "farm_id"       => req.SortDesc ? query.OrderByDescending(x => x.FarmId)        : query.OrderBy(x => x.FarmId),
            "lote_id"       => req.SortDesc ? query.OrderByDescending(x => x.LoteId)        : query.OrderBy(x => x.LoteId),
            "edad_dias"     => req.SortDesc ? query.OrderByDescending(x => x.EdadDias)      : query.OrderBy(x => x.EdadDias),
            "id"            => req.SortDesc ? query.OrderByDescending(x => x.Id)            : query.OrderBy(x => x.Id),
            _               => req.SortDesc ? query.OrderByDescending(x => x.FechaRegistro) : query.OrderBy(x => x.FechaRegistro)
        };

        var total = await query.LongCountAsync(ct);
        var items = await query
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(x => ToDto(x))
            .ToListAsync(ct);

        return new PagedResult<LesionDto>
        {
            Page     = req.Page,
            PageSize = req.PageSize,
            Total    = total,
            Items    = items
        };
    }

    public async Task<LesionDto> CreateAsync(CreateLesionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TipoLesion))
            throw new InvalidOperationException("TipoLesion es requerido.");

        if (string.IsNullOrWhiteSpace(req.ModuloOrigen) || !ModulosValidos.Contains(req.ModuloOrigen))
            throw new InvalidOperationException("ModuloOrigen debe ser 'REPRODUCTORA', 'APOYO' o 'ENGORDE'.");

        var farmExists = await _ctx.Farms.AsNoTracking()
            .AnyAsync(f => f.Id == req.FarmId, ct);
        if (!farmExists)
            throw new InvalidOperationException($"La granja con Id {req.FarmId} no existe.");

        var companyId = await GetEffectiveCompanyIdAsync();
        var now = DateTime.UtcNow;

        var entity = new Lesion
        {
            CompanyId          = companyId,
            CreatedByUserId    = _currentUser.UserId,
            CreatedAt          = now,
            ClienteId          = req.ClienteId,
            FarmId             = req.FarmId,
            GalponId           = req.GalponId,
            LoteId             = req.LoteId,
            LoteReproductoraId = req.LoteReproductoraId,
            EdadDias           = req.EdadDias,
            AvesMacho          = req.AvesMacho,
            AvesHembra         = req.AvesHembra,
            AvesMixtas         = req.AvesMixtas,
            TipoLesion         = req.TipoLesion.Trim(),
            Observaciones      = req.Observaciones,
            FechaRegistro      = now,
            ModuloOrigen       = req.ModuloOrigen.ToUpper(),
            Status             = "A"
        };

        _ctx.Lesiones.Add(entity);
        await _ctx.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<LesionDto?> UpdateAsync(long id, UpdateLesionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TipoLesion))
            throw new InvalidOperationException("TipoLesion es requerido.");

        if (string.IsNullOrWhiteSpace(req.ModuloOrigen) || !ModulosValidos.Contains(req.ModuloOrigen))
            throw new InvalidOperationException("ModuloOrigen debe ser 'REPRODUCTORA', 'APOYO' o 'ENGORDE'.");

        var companyId = await GetEffectiveCompanyIdAsync();
        var entity = await _ctx.Lesiones
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && x.DeletedAt == null, ct);

        if (entity is null) return null;

        var farmExists = await _ctx.Farms.AsNoTracking()
            .AnyAsync(f => f.Id == req.FarmId, ct);
        if (!farmExists)
            throw new InvalidOperationException($"La granja con Id {req.FarmId} no existe.");

        entity.ClienteId          = req.ClienteId;
        entity.FarmId             = req.FarmId;
        entity.GalponId           = req.GalponId;
        entity.LoteId             = req.LoteId;
        entity.LoteReproductoraId = req.LoteReproductoraId;
        entity.EdadDias           = req.EdadDias;
        entity.AvesMacho          = req.AvesMacho;
        entity.AvesHembra         = req.AvesHembra;
        entity.AvesMixtas         = req.AvesMixtas;
        entity.TipoLesion         = req.TipoLesion.Trim();
        entity.Observaciones      = req.Observaciones;
        entity.ModuloOrigen       = req.ModuloOrigen.ToUpper();
        entity.Status             = req.Status;
        entity.UpdatedByUserId    = _currentUser.UserId;
        entity.UpdatedAt          = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var entity = await _ctx.Lesiones
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && x.DeletedAt == null, ct);

        if (entity is null) return false;

        var now = DateTime.UtcNow;
        entity.DeletedAt       = now;
        entity.Status          = "I";
        entity.UpdatedByUserId = _currentUser.UserId;
        entity.UpdatedAt       = now;

        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IEnumerable<LesionResumenDto>> GetResumenAsync(
        string? moduloOrigen,
        int?    clienteId,
        int?    farmId,
        int?    loteId,
        string? galponId,
        string? loteReproductoraId,
        CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        var query = _ctx.Lesiones
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.DeletedAt == null && x.Status == "A");

        if (!string.IsNullOrWhiteSpace(moduloOrigen))
        {
            var m = moduloOrigen.ToUpper();
            query = query.Where(x => x.ModuloOrigen == m);
        }

        if (clienteId.HasValue)
            query = query.Where(x => x.ClienteId == clienteId.Value);

        if (farmId.HasValue)
            query = query.Where(x => x.FarmId == farmId.Value);

        if (loteId.HasValue)
            query = query.Where(x => x.LoteId == loteId.Value);

        if (!string.IsNullOrWhiteSpace(galponId))
            query = query.Where(x => x.GalponId == galponId);

        if (!string.IsNullOrWhiteSpace(loteReproductoraId))
            query = query.Where(x => x.LoteReproductoraId == loteReproductoraId);

        // Usar anonymous type en el Select del GroupBy para que EF Core pueda
        // traducirlo a SQL sin generar .AsQueryable() no traducible.
        var raw = await query
            .GroupBy(x => new { x.TipoLesion, x.ModuloOrigen })
            .Select(g => new
            {
                g.Key.TipoLesion,
                g.Key.ModuloOrigen,
                TotalRegistros  = g.Count(),
                TotalAvesMacho  = g.Sum(x => x.AvesMacho  ?? 0),
                TotalAvesHembra = g.Sum(x => x.AvesHembra ?? 0),
                TotalAvesMixtas = g.Sum(x => x.AvesMixtas ?? 0)
            })
            .OrderBy(r => r.ModuloOrigen)
            .ThenBy(r => r.TipoLesion)
            .ToListAsync(ct);

        return raw.Select(r => new LesionResumenDto(
            r.TipoLesion,
            r.ModuloOrigen,
            r.TotalRegistros,
            r.TotalAvesMacho,
            r.TotalAvesHembra,
            r.TotalAvesMixtas
        ));
    }

    private static LesionDto ToDto(Lesion x) => new(
        x.Id,
        x.ClienteId,
        x.FarmId,
        x.GalponId,
        x.LoteId,
        x.LoteReproductoraId,
        x.EdadDias,
        x.AvesMacho,
        x.AvesHembra,
        x.AvesMixtas,
        x.TipoLesion,
        x.Observaciones,
        x.FechaRegistro,
        x.ModuloOrigen,
        x.Status,
        x.CompanyId,
        x.CreatedByUserId,
        x.CreatedAt,
        x.UpdatedByUserId,
        x.UpdatedAt
    );
}
