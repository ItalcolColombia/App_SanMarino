using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ProduccionDiariaService : IProduccionDiariaService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;

    public ProduccionDiariaService(ZooSanMarinoContext ctx, ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    public async Task<IEnumerable<ProduccionDiariaDto>> GetAllAsync()
    {
        var q = from p in _ctx.ProduccionDiaria.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking() on p.LoteId equals l.LoteId.ToString()
                where l.CompanyId == _current.CompanyId && l.DeletedAt == null
                select p;

        return await q
            .OrderByDescending(x => x.FechaRegistro)
            .Select(x => new ProduccionDiariaDto(
                x.Id,
                x.LoteId,
                x.FechaRegistro,
                x.MortalidadHembras,
                x.MortalidadMachos,
                x.SelH,
                x.ConsKgH,
                x.ConsKgM,
                x.HuevoTot,
                x.HuevoInc,
                x.TipoAlimento,
                x.Observaciones,
                x.PesoHuevo,
                x.Etapa
            ))
            .ToListAsync();
    }

    public async Task<IEnumerable<ProduccionDiariaDto>> GetByLoteIdAsync(int loteId)
    {
        var q = from p in _ctx.ProduccionDiaria.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking() on p.LoteId equals l.LoteId.ToString()
                where l.CompanyId == _current.CompanyId && 
                      l.DeletedAt == null && 
                      l.LoteId == loteId
                select p;

        return await q
            .OrderByDescending(x => x.FechaRegistro)
            .Select(x => new ProduccionDiariaDto(
                x.Id,
                x.LoteId,
                x.FechaRegistro,
                x.MortalidadHembras,
                x.MortalidadMachos,
                x.SelH,
                x.ConsKgH,
                x.ConsKgM,
                x.HuevoTot,
                x.HuevoInc,
                x.TipoAlimento,
                x.Observaciones,
                x.PesoHuevo,
                x.Etapa
            ))
            .ToListAsync();
    }

    public async Task<ProduccionDiariaDto> CreateAsync(CreateProduccionDiariaDto dto)
    {
        // Verificar que el lote existe y pertenece a la compañía
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId.ToString() == dto.LoteId &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        // VALIDACIÓN CRÍTICA: Verificar que existe un ProduccionLote configurado para este lote
        var produccionLote = await _ctx.ProduccionLotes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LoteId.ToString() == dto.LoteId);
        if (produccionLote is null)
            throw new InvalidOperationException($"El lote '{dto.LoteId}' no tiene configuración de producción inicial. Debe crear primero el registro de ProduccionLote.");

        // Verificar que no existe ya un registro para la misma fecha y lote
        var existeRegistro = await _ctx.ProduccionDiaria.AsNoTracking()
            .AnyAsync(p => p.LoteId == dto.LoteId && 
                          p.FechaRegistro.Date == dto.FechaRegistro.Date);
        if (existeRegistro)
            throw new InvalidOperationException($"Ya existe un registro de producción diaria para el lote '{dto.LoteId}' en la fecha '{dto.FechaRegistro:yyyy-MM-dd}'.");

        var entity = new Domain.Entities.ProduccionDiaria
        {
            LoteId = dto.LoteId,
            FechaRegistro = dto.FechaRegistro,
            MortalidadHembras = dto.MortalidadHembras,
            MortalidadMachos = dto.MortalidadMachos,
            SelH = dto.SelH,
            ConsKgH = dto.ConsKgH,
            ConsKgM = dto.ConsKgM,
            HuevoTot = dto.HuevoTot,
            HuevoInc = dto.HuevoInc,
            TipoAlimento = dto.TipoAlimento,
            Observaciones = dto.Observaciones,
            PesoHuevo = dto.PesoHuevo,
            Etapa = dto.Etapa
        };

        _ctx.ProduccionDiaria.Add(entity);
        await _ctx.SaveChangesAsync();

        return new ProduccionDiariaDto(
            entity.Id,
            entity.LoteId,
            entity.FechaRegistro,
            entity.MortalidadHembras,
            entity.MortalidadMachos,
            entity.SelH,
            entity.ConsKgH,
            entity.ConsKgM,
            entity.HuevoTot,
            entity.HuevoInc,
            entity.TipoAlimento,
            entity.Observaciones,
            entity.PesoHuevo,
            entity.Etapa
        );
    }

    public async Task<ProduccionDiariaDto?> UpdateAsync(UpdateProduccionDiariaDto dto)
    {
        var entity = await _ctx.ProduccionDiaria.FindAsync(dto.Id);
        if (entity == null) return null;

        // Verificar que el lote existe y pertenece a la compañía
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId.ToString() == dto.LoteId &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        // Verificar que no existe ya otro registro para la misma fecha y lote (excluyendo el actual)
        var existeOtroRegistro = await _ctx.ProduccionDiaria.AsNoTracking()
            .AnyAsync(p => p.LoteId == dto.LoteId && 
                          p.FechaRegistro.Date == dto.FechaRegistro.Date &&
                          p.Id != dto.Id);
        if (existeOtroRegistro)
            throw new InvalidOperationException($"Ya existe otro registro de producción diaria para el lote '{dto.LoteId}' en la fecha '{dto.FechaRegistro:yyyy-MM-dd}'.");

        entity.LoteId = dto.LoteId;
        entity.FechaRegistro = dto.FechaRegistro;
        entity.MortalidadHembras = dto.MortalidadHembras;
        entity.MortalidadMachos = dto.MortalidadMachos;
        entity.SelH = dto.SelH;
        entity.ConsKgH = dto.ConsKgH;
        entity.ConsKgM = dto.ConsKgM;
        entity.HuevoTot = dto.HuevoTot;
        entity.HuevoInc = dto.HuevoInc;
        entity.TipoAlimento = dto.TipoAlimento;
        entity.Observaciones = dto.Observaciones;
        entity.PesoHuevo = dto.PesoHuevo;
        entity.Etapa = dto.Etapa;

        await _ctx.SaveChangesAsync();

        return new ProduccionDiariaDto(
            entity.Id,
            entity.LoteId,
            entity.FechaRegistro,
            entity.MortalidadHembras,
            entity.MortalidadMachos,
            entity.SelH,
            entity.ConsKgH,
            entity.ConsKgM,
            entity.HuevoTot,
            entity.HuevoInc,
            entity.TipoAlimento,
            entity.Observaciones,
            entity.PesoHuevo,
            entity.Etapa
        );
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _ctx.ProduccionDiaria.FindAsync(id);
        if (entity == null) return false;

        // Verificar que el lote pertenece a la compañía
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId.ToString() == entity.LoteId &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null) return false;

        _ctx.ProduccionDiaria.Remove(entity);
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ProduccionDiariaDto>> FilterAsync(FilterProduccionDiariaDto filter)
    {
        var q = from p in _ctx.ProduccionDiaria.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking() on p.LoteId equals l.LoteId.ToString()
                where l.CompanyId == _current.CompanyId && l.DeletedAt == null
                select p;

        if (!string.IsNullOrWhiteSpace(filter.LoteId))
            q = q.Where(x => x.LoteId == filter.LoteId);
        if (filter.Desde.HasValue)
            q = q.Where(x => x.FechaRegistro >= filter.Desde.Value);
        if (filter.Hasta.HasValue)
            q = q.Where(x => x.FechaRegistro <= filter.Hasta.Value);

        return await q
            .OrderByDescending(x => x.FechaRegistro)
            .Select(x => new ProduccionDiariaDto(
                x.Id,
                x.LoteId,
                x.FechaRegistro,
                x.MortalidadHembras,
                x.MortalidadMachos,
                x.SelH,
                x.ConsKgH,
                x.ConsKgM,
                x.HuevoTot,
                x.HuevoInc,
                x.TipoAlimento,
                x.Observaciones,
                x.PesoHuevo,
                x.Etapa
            ))
            .ToListAsync();
    }

    public async Task<bool> HasProduccionLoteConfigAsync(string loteId)
    {
        // Verificar que el lote existe y pertenece a la compañía
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId.ToString() == loteId &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null) return false;

        // Verificar si existe un ProduccionLote configurado para este lote
        var produccionLote = await _ctx.ProduccionLotes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LoteId.ToString() == loteId);
        
        return produccionLote is not null;
    }
}
