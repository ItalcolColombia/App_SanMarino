using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoProduccionService : ISeguimientoProduccionService
{
    private readonly ZooSanMarinoContext _ctx;

    public SeguimientoProduccionService(ZooSanMarinoContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<IEnumerable<SeguimientoProduccionDto>> GetAllAsync()
    {
        return await _ctx.SeguimientoProduccion
            .Select(x => new SeguimientoProduccionDto(
                x.Id,
                x.Fecha,
                x.LoteId,
                x.MortalidadH,
                x.MortalidadM,
                x.SelH,
                x.ConsKgH,
                x.ConsKgM,
                x.HuevoTot,
                x.HuevoInc,
                x.TipoAlimento,
                x.Observaciones ?? "",
                x.PesoHuevo,
                x.Etapa,
                x.Metadata
            ))
            .ToListAsync();
    }

    public async Task<SeguimientoProduccionDto?> GetByLoteIdAsync(int loteId)
    {
        var loteIdStr = loteId.ToString();
        var entity = await _ctx.SeguimientoProduccion
            .Where(x => x.LoteId == loteIdStr)
            .OrderByDescending(x => x.Fecha)
            .FirstOrDefaultAsync();

        if (entity == null) return null;

        return new SeguimientoProduccionDto(
            entity.Id,
            entity.Fecha,
            entity.LoteId,
            entity.MortalidadH,
            entity.MortalidadM,
            entity.SelH,
            entity.ConsKgH,
            entity.ConsKgM,
            entity.HuevoTot,
            entity.HuevoInc,
            entity.TipoAlimento,
            entity.Observaciones ?? "",
            entity.PesoHuevo,
            entity.Etapa,
            entity.Metadata
        );
    }

    public async Task<SeguimientoProduccionDto> CreateAsync(CreateSeguimientoProduccionDto dto)
    {
        var entity = new Domain.Entities.SeguimientoProduccion
        {
            Fecha = dto.Fecha,
            LoteId = dto.LoteId.ToString(),
            MortalidadH = dto.MortalidadH,
            MortalidadM = dto.MortalidadM,
            SelH = dto.SelH,
            ConsKgH = dto.ConsKgH,
            ConsKgM = dto.ConsKgM,
            HuevoTot = dto.HuevoTot,
            HuevoInc = dto.HuevoInc,
            TipoAlimento = dto.TipoAlimento,
            Observaciones = dto.Observaciones,
            PesoHuevo = dto.PesoHuevo,
            Etapa = dto.Etapa,
            Metadata = dto.Metadata
        };

        _ctx.SeguimientoProduccion.Add(entity);
        await _ctx.SaveChangesAsync();

        return new SeguimientoProduccionDto(
            entity.Id,
            entity.Fecha,
            entity.LoteId,
            entity.MortalidadH,
            entity.MortalidadM,
            entity.SelH,
            entity.ConsKgH,
            entity.ConsKgM,
            entity.HuevoTot,
            entity.HuevoInc,
            entity.TipoAlimento,
            entity.Observaciones ?? "",
            entity.PesoHuevo,
            entity.Etapa,
            entity.Metadata
        );
    }

    public async Task<SeguimientoProduccionDto?> UpdateAsync(UpdateSeguimientoProduccionDto dto)
    {
        var entity = await _ctx.SeguimientoProduccion.FindAsync(dto.Id);
        if (entity == null) return null;

        entity.Fecha = dto.Fecha;
        entity.LoteId = dto.LoteId.ToString();
        entity.MortalidadH = dto.MortalidadH;
        entity.MortalidadM = dto.MortalidadM;
        entity.SelH = dto.SelH;
        entity.ConsKgH = dto.ConsKgH;
        entity.ConsKgM = dto.ConsKgM;
        entity.HuevoTot = dto.HuevoTot;
        entity.HuevoInc = dto.HuevoInc;
        entity.TipoAlimento = dto.TipoAlimento;
        entity.Observaciones = dto.Observaciones;
        entity.PesoHuevo = dto.PesoHuevo;
        entity.Etapa = dto.Etapa;
        entity.Metadata = dto.Metadata;

        await _ctx.SaveChangesAsync();

        return new SeguimientoProduccionDto(
            entity.Id,
            entity.Fecha,
            entity.LoteId,
            entity.MortalidadH,
            entity.MortalidadM,
            entity.SelH,
            entity.ConsKgH,
            entity.ConsKgM,
            entity.HuevoTot,
            entity.HuevoInc,
            entity.TipoAlimento,
            entity.Observaciones ?? "",
            entity.PesoHuevo,
            entity.Etapa,
            entity.Metadata
        );
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _ctx.SeguimientoProduccion.FindAsync(id);
        if (entity == null) return false;

        _ctx.SeguimientoProduccion.Remove(entity);
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<SeguimientoProduccionDto>> FilterAsync(FilterSeguimientoProduccionDto filter)
    {
        var query = _ctx.SeguimientoProduccion.AsQueryable();

        if (filter.LoteId.HasValue)
            query = query.Where(x => x.LoteId == filter.LoteId.Value.ToString());

        if (filter.Desde.HasValue)
            query = query.Where(x => x.Fecha >= filter.Desde.Value);

        if (filter.Hasta.HasValue)
            query = query.Where(x => x.Fecha <= filter.Hasta.Value);

        return await query
            .OrderByDescending(x => x.Fecha)
            .Select(x => new SeguimientoProduccionDto(
                x.Id,
                x.Fecha,
                x.LoteId,
                x.MortalidadH,
                x.MortalidadM,
                x.SelH,
                x.ConsKgH,
                x.ConsKgM,
                x.HuevoTot,
                x.HuevoInc,
                x.TipoAlimento,
                x.Observaciones ?? "",
                x.PesoHuevo,
                x.Etapa,
                x.Metadata
            ))
            .ToListAsync();
    }
}
