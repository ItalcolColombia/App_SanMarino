// file: src/ZooSanMarino.Infrastructure/Services/ProduccionLoteService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using AppInterfaces = ZooSanMarino.Application.Interfaces; // IProduccionLoteService, ICurrentUser
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ProduccionLoteService : AppInterfaces.IProduccionLoteService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly AppInterfaces.ICurrentUser _current;

    public ProduccionLoteService(ZooSanMarinoContext ctx, AppInterfaces.ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    public async Task<ProduccionLoteDto> CreateAsync(CreateProduccionLoteDto dto)
    {
        // Lote del tenant y activo
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == dto.LoteId &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        // Núcleo válido en la misma granja y tenant
        var nucleoOk = await _ctx.Nucleos.AsNoTracking()
            .AnyAsync(n => n.NucleoId == dto.NucleoProduccionId &&
                           n.GranjaId == dto.GranjaId &&
                           n.CompanyId == _current.CompanyId &&
                           n.DeletedAt == null);
        if (!nucleoOk)
            throw new InvalidOperationException("Núcleo de producción no existe en la granja o no pertenece a la compañía.");

        if (lote.GranjaId != dto.GranjaId)
            throw new InvalidOperationException("El Lote no pertenece a la granja indicada para producción.");

        var ent = new ProduccionLote
        {
            LoteId = dto.LoteId,
            FechaInicio = dto.FechaInicioProduccion,
            AvesInicialesH = dto.HembrasIniciales,
            AvesInicialesM = dto.MachosIniciales,
            Observaciones = dto.TipoNido
        };

        _ctx.ProduccionLotes.Add(ent);
        await _ctx.SaveChangesAsync();

        return new ProduccionLoteDto(
            ent.Id, ent.LoteId, ent.FechaInicio,
            ent.AvesInicialesH, ent.AvesInicialesM, 0, // HuevosIniciales = 0 para compatibilidad
            ent.Observaciones ?? "", // TipoNido
            "", // NucleoId vacío para compatibilidad
            dto.GranjaId, dto.Ciclo
        );
    }

    public async Task<IEnumerable<ProduccionLoteDto>> GetAllAsync()
    {
        var q = from p in _ctx.ProduccionLotes.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking() on p.LoteId equals l.LoteId
                where l.CompanyId == _current.CompanyId && l.DeletedAt == null
                select p;

        return await q
            .Select(x => new ProduccionLoteDto(
                x.Id, x.LoteId, x.FechaInicio,
                x.AvesInicialesH, x.AvesInicialesM, 0, // HuevosIniciales = 0
                x.Observaciones ?? "", "", // NucleoId vacío
                0, "" // GranjaId = 0, Ciclo vacío
            ))
            .ToListAsync();
    }

    public async Task<ProduccionLoteDto?> GetByLoteIdAsync(int loteId)
    {
        var q = from p in _ctx.ProduccionLotes.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking() on p.LoteId equals l.LoteId
                where l.CompanyId == _current.CompanyId && l.DeletedAt == null && p.LoteId == loteId
                select p;

        var x = await q.OrderByDescending(p => p.FechaInicio).FirstOrDefaultAsync();
        return x is null ? null
            : new ProduccionLoteDto(
                x.Id, x.LoteId, x.FechaInicio,
                x.AvesInicialesH, x.AvesInicialesM, 0,
                x.Observaciones ?? "", "", 0, ""
            );
    }

    public async Task<ProduccionLoteDto?> UpdateAsync(UpdateProduccionLoteDto dto)
    {
        var ent = await _ctx.ProduccionLotes.FindAsync(dto.Id);
        if (ent is null) return null;

        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == dto.LoteId &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        var nucleoOk = await _ctx.Nucleos.AsNoTracking()
            .AnyAsync(n => n.NucleoId == dto.NucleoProduccionId &&
                           n.GranjaId == dto.GranjaId &&
                           n.CompanyId == _current.CompanyId &&
                           n.DeletedAt == null);
        if (!nucleoOk)
            throw new InvalidOperationException("Núcleo de producción no existe en la granja o no pertenece a la compañía.");

        if (lote.GranjaId != dto.GranjaId)
            throw new InvalidOperationException("El Lote no pertenece a la granja indicada para producción.");

        ent.LoteId = dto.LoteId;
        ent.FechaInicio = dto.FechaInicioProduccion;
        ent.AvesInicialesH = dto.HembrasIniciales;
        ent.AvesInicialesM = dto.MachosIniciales;
        ent.Observaciones = dto.TipoNido;

        await _ctx.SaveChangesAsync();

        return new ProduccionLoteDto(
            ent.Id, ent.LoteId, ent.FechaInicio,
            ent.AvesInicialesH, ent.AvesInicialesM, 0,
            ent.Observaciones ?? "", "", 0, ""
        );
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var ent = await _ctx.ProduccionLotes.FindAsync(id);
        if (ent is null) return false;

        var ok = await _ctx.Lotes.AsNoTracking()
            .AnyAsync(l => l.LoteId == ent.LoteId && l.CompanyId == _current.CompanyId);
        if (!ok) return false;

        _ctx.ProduccionLotes.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ProduccionLoteDto>> FilterAsync(FilterProduccionLoteDto filter)
    {
        var q = from p in _ctx.ProduccionLotes.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking() on p.LoteId equals l.LoteId
                where l.CompanyId == _current.CompanyId && l.DeletedAt == null
                select p;

        if (filter.LoteId.HasValue)
            q = q.Where(x => x.LoteId == filter.LoteId.Value);
        if (filter.Desde.HasValue)
            q = q.Where(x => x.FechaInicio >= filter.Desde.Value);
        if (filter.Hasta.HasValue)
            q = q.Where(x => x.FechaInicio <= filter.Hasta.Value);

        return await q
            .OrderBy(x => x.LoteId).ThenBy(x => x.FechaInicio)
            .Select(x => new ProduccionLoteDto(
                x.Id, x.LoteId, x.FechaInicio,
                x.AvesInicialesH, x.AvesInicialesM, 0,
                x.Observaciones ?? "", "", 0, ""
            ))
            .ToListAsync();
    }
}
