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
        var items = await _ctx.LotePosturaBases
            .AsNoTracking()
            .Where(x => x.CompanyId == _current.CompanyId && x.DeletedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return items.Select(Map).ToList();
    }

    public async Task<LotePosturaBaseDto?> GetByIdAsync(int id)
    {
        var e = await _ctx.LotePosturaBases
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.LotePosturaBaseId == id &&
                                       x.CompanyId == _current.CompanyId &&
                                       x.DeletedAt == null);
        return e is null ? null : Map(e);
    }

    public async Task<LotePosturaBaseDto> CreateAsync(CreateLotePosturaBaseDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.LoteNombre))
            throw new InvalidOperationException("El nombre del lote es requerido.");
        if (dto.CantidadHembras < 0 || dto.CantidadMachos < 0 || dto.CantidadMixtas < 0)
            throw new InvalidOperationException("Las cantidades no pueden ser negativas.");

        var e = new LotePosturaBase
        {
            CompanyId = _current.CompanyId,
            CreatedByUserId = _current.UserId,
            PaisId = _current.PaisId,
            LoteNombre = dto.LoteNombre.Trim(),
            CodigoErp = string.IsNullOrWhiteSpace(dto.CodigoErp) ? null : dto.CodigoErp.Trim(),
            CantidadHembras = dto.CantidadHembras,
            CantidadMachos = dto.CantidadMachos,
            CantidadMixtas = dto.CantidadMixtas,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.LotePosturaBases.Add(e);
        await _ctx.SaveChangesAsync();

        return Map(e);
    }

    private static LotePosturaBaseDto Map(LotePosturaBase e) =>
        new(
            e.LotePosturaBaseId,
            e.LoteNombre,
            e.CodigoErp,
            e.CantidadHembras,
            e.CantidadMachos,
            e.CantidadMixtas,
            e.CompanyId,
            e.CreatedByUserId,
            e.PaisId,
            e.CreatedAt
        );
}

