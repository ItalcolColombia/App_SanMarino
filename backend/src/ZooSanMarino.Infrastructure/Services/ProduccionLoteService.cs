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


        var ent = new ProduccionLote
        {
            LoteId = dto.LoteId.ToString(), // Convertir int a string
            FechaInicio = dto.FechaInicioProduccion,
            AvesInicialesH = dto.HembrasIniciales,
            AvesInicialesM = dto.MachosIniciales,
            HuevosIniciales = dto.HuevosIniciales,
            TipoNido = dto.TipoNido,
            Ciclo = dto.Ciclo
        };

        _ctx.ProduccionLotes.Add(ent);
        await _ctx.SaveChangesAsync();

        return new ProduccionLoteDto(
            ent.Id, int.Parse(ent.LoteId), ent.FechaInicio, // Convertir string a int
            ent.AvesInicialesH, ent.AvesInicialesM, 
            ent.HuevosIniciales, ent.TipoNido,
            ent.Ciclo
        );
    }

    public async Task<IEnumerable<ProduccionLoteDto>> GetAllAsync()
    {
        var q = from p in _ctx.ProduccionLotes.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking() on int.Parse(p.LoteId) equals l.LoteId
                where l.CompanyId == _current.CompanyId && l.DeletedAt == null
                select p;

        return await q
            .Select(x => new ProduccionLoteDto(
                x.Id, int.Parse(x.LoteId), x.FechaInicio, // Convertir string a int
                x.AvesInicialesH, x.AvesInicialesM, 
                x.HuevosIniciales, x.TipoNido,
                x.Ciclo
            ))
            .ToListAsync();
    }

    public async Task<ProduccionLoteDto?> GetByLoteIdAsync(int loteId)
    {
        var loteIdStr = loteId.ToString();
        var q = from p in _ctx.ProduccionLotes.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking() on int.Parse(p.LoteId) equals l.LoteId
                where l.CompanyId == _current.CompanyId && l.DeletedAt == null && p.LoteId == loteIdStr
                select p;

        var x = await q.OrderByDescending(p => p.FechaInicio).FirstOrDefaultAsync();
        return x is null ? null
            : new ProduccionLoteDto(
                x.Id, int.Parse(x.LoteId), x.FechaInicio, // Convertir string a int
                x.AvesInicialesH, x.AvesInicialesM, 
                x.HuevosIniciales, x.TipoNido,
                x.Ciclo
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


        ent.LoteId = dto.LoteId.ToString(); // Convertir int a string
        ent.FechaInicio = dto.FechaInicioProduccion;
        ent.AvesInicialesH = dto.HembrasIniciales;
        ent.AvesInicialesM = dto.MachosIniciales;
        ent.HuevosIniciales = dto.HuevosIniciales;
        ent.TipoNido = dto.TipoNido;
        ent.Ciclo = dto.Ciclo;

        await _ctx.SaveChangesAsync();

        return new ProduccionLoteDto(
            ent.Id, int.Parse(ent.LoteId), ent.FechaInicio, // Convertir string a int
            ent.AvesInicialesH, ent.AvesInicialesM, 
            ent.HuevosIniciales, ent.TipoNido,
            ent.Ciclo
        );
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var ent = await _ctx.ProduccionLotes.FindAsync(id);
        if (ent is null) return false;

        var ok = await _ctx.Lotes.AsNoTracking()
            .AnyAsync(l => l.LoteId == int.Parse(ent.LoteId) && l.CompanyId == _current.CompanyId);
        if (!ok) return false;

        _ctx.ProduccionLotes.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ProduccionLoteDto>> FilterAsync(FilterProduccionLoteDto filter)
    {
        var q = from p in _ctx.ProduccionLotes.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking() on int.Parse(p.LoteId) equals l.LoteId
                where l.CompanyId == _current.CompanyId && l.DeletedAt == null
                select p;

        if (filter.LoteId.HasValue)
            q = q.Where(x => x.LoteId == filter.LoteId.Value.ToString());
        if (filter.Desde.HasValue)
            q = q.Where(x => x.FechaInicio >= filter.Desde.Value);
        if (filter.Hasta.HasValue)
            q = q.Where(x => x.FechaInicio <= filter.Hasta.Value);

        return await q
            .OrderBy(x => x.LoteId).ThenBy(x => x.FechaInicio)
            .Select(x => new ProduccionLoteDto(
                x.Id, int.Parse(x.LoteId), x.FechaInicio, // Convertir string a int
                x.AvesInicialesH, x.AvesInicialesM, x.HuevosIniciales,
                x.TipoNido, x.Ciclo
            ))
            .ToListAsync();
    }
}
