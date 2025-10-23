// file: src/ZooSanMarino.Infrastructure/Services/LoteSeguimientoService.cs
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using ZooSanMarino.Application.DTOs;
using AppInterfaces = ZooSanMarino.Application.Interfaces; // ILoteSeguimientoService, ICurrentUser
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LoteSeguimientoService : AppInterfaces.ILoteSeguimientoService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly AppInterfaces.ICurrentUser _current;

    public LoteSeguimientoService(ZooSanMarinoContext ctx, AppInterfaces.ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    // Mapeo centralizado
    private static readonly Expression<Func<LoteSeguimiento, LoteSeguimientoDto>> ToDto =
        x => new LoteSeguimientoDto(
            x.Id,
            x.Fecha,
            x.LoteId,
            x.ReproductoraId,
            x.PesoInicial,
            x.PesoFinal,
            x.MortalidadM,
            x.MortalidadH,
            x.SelM,
            x.SelH,
            x.ErrorM,
            x.ErrorH,
            x.TipoAlimento,
            x.ConsumoAlimento,
            x.Observaciones
        );

    // ======================================================
    // LISTADO (solo registros cuyo Lote pertenece a la compañía actual)
    // ======================================================
    public async Task<IEnumerable<LoteSeguimientoDto>> GetAllAsync()
    {
        var q = from s in _ctx.LoteSeguimientos.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking()
                    on s.LoteId equals l.LoteId
                where l.CompanyId == _current.CompanyId && l.DeletedAt == null
                select s;

        return await q
            .OrderBy(x => x.LoteId).ThenBy(x => x.Fecha)
            .Select(ToDto)
            .ToListAsync();
    }

    // ======================================================
    // GET por Id (validando tenant via join a Lote)
    // ======================================================
    public async Task<LoteSeguimientoDto?> GetByIdAsync(int id)
    {
        var q = from s in _ctx.LoteSeguimientos.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking()
                    on s.LoteId equals l.LoteId
                where l.CompanyId == _current.CompanyId && l.DeletedAt == null && s.Id == id
                select s;

        return await q.Select(ToDto).SingleOrDefaultAsync();
    }

    // ======================================================
    // CREATE (valida Lote y LoteReproductora)
    // ======================================================
    public async Task<LoteSeguimientoDto> CreateAsync(CreateLoteSeguimientoDto dto)
    {
        // 1) Validar Lote en la compañía
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == dto.LoteId &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        // 2) Validar que la Reproductora exista para ese Lote (y, por transitividad, en el mismo tenant)
        var existeReproductora = await (from lr in _ctx.LoteReproductoras.AsNoTracking()
                                        join l in _ctx.Lotes.AsNoTracking()
                                            on lr.LoteId equals l.LoteId
                                        where lr.LoteId == dto.LoteId &&
                                              lr.ReproductoraId == dto.ReproductoraId &&
                                              l.CompanyId == _current.CompanyId &&
                                              l.DeletedAt == null
                                        select 1).AnyAsync();
        if (!existeReproductora)
            throw new InvalidOperationException("La Reproductora indicada no existe para ese Lote.");

        // 3) (Opcional) Enforzar unicidad Lote+Reproductora+Fecha
        var duplicado = await _ctx.LoteSeguimientos.AsNoTracking()
            .AnyAsync(x => x.LoteId == dto.LoteId &&
                           x.ReproductoraId == dto.ReproductoraId &&
                           x.Fecha.Date == dto.Fecha.Date);
        if (duplicado)
            throw new InvalidOperationException("Ya existe un seguimiento para ese Lote, Reproductora y Fecha.");

        var ent = new LoteSeguimiento
        {
            Fecha           = dto.Fecha,
            LoteId          = dto.LoteId,
            ReproductoraId  = dto.ReproductoraId,
            PesoInicial     = dto.PesoInicial,
            PesoFinal       = dto.PesoFinal,
            MortalidadM     = dto.MortalidadM,
            MortalidadH     = dto.MortalidadH,
            SelM            = dto.SelM,
            SelH            = dto.SelH,
            ErrorM          = dto.ErrorM,
            ErrorH          = dto.ErrorH,
            TipoAlimento    = dto.TipoAlimento,
            ConsumoAlimento = dto.ConsumoAlimento,
            Observaciones   = dto.Observaciones
        };

        _ctx.LoteSeguimientos.Add(ent);
        await _ctx.SaveChangesAsync();

        return await GetByIdAsync(ent.Id) ?? // vuelve a pasar por el filtro de tenant
               new LoteSeguimientoDto(
                   ent.Id, ent.Fecha, ent.LoteId, ent.ReproductoraId, ent.PesoInicial, ent.PesoFinal,
                   ent.MortalidadM, ent.MortalidadH, ent.SelM, ent.SelH, ent.ErrorM, ent.ErrorH,
                   ent.TipoAlimento, ent.ConsumoAlimento, ent.Observaciones);
    }

    // ======================================================
    // UPDATE (valida cambios de Lote/Reproductora y duplicados)
    // ======================================================
    public async Task<LoteSeguimientoDto?> UpdateAsync(UpdateLoteSeguimientoDto dto)
    {
        var ent = await _ctx.LoteSeguimientos.FindAsync(dto.Id);
        if (ent is null) return null;

        // Validar que el nuevo Lote pertenezca a la compañía
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == dto.LoteId &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        // Validar Reproductora del Lote
        var existeReproductora = await (from lr in _ctx.LoteReproductoras.AsNoTracking()
                                        join l in _ctx.Lotes.AsNoTracking()
                                            on lr.LoteId equals l.LoteId
                                        where lr.LoteId == dto.LoteId &&
                                              lr.ReproductoraId == dto.ReproductoraId &&
                                              l.CompanyId == _current.CompanyId &&
                                              l.DeletedAt == null
                                        select 1).AnyAsync();
        if (!existeReproductora)
            throw new InvalidOperationException("La Reproductora indicada no existe para ese Lote.");

        // Si cambia la clave Lote/Reproductora/Fecha, impedir duplicado
        var cambiaClave = ent.LoteId != dto.LoteId ||
                          ent.ReproductoraId != dto.ReproductoraId ||
                          ent.Fecha.Date != dto.Fecha.Date;
        if (cambiaClave)
        {
            var existeOtro = await _ctx.LoteSeguimientos.AsNoTracking()
                .AnyAsync(x => x.Id != dto.Id &&
                               x.LoteId == dto.LoteId &&
                               x.ReproductoraId == dto.ReproductoraId &&
                               x.Fecha.Date == dto.Fecha.Date);
            if (existeOtro)
                throw new InvalidOperationException("Ya existe un seguimiento para ese Lote, Reproductora y Fecha.");
        }

        ent.Fecha           = dto.Fecha;
        ent.LoteId          = dto.LoteId;
        ent.ReproductoraId  = dto.ReproductoraId;
        ent.PesoInicial     = dto.PesoInicial;
        ent.PesoFinal       = dto.PesoFinal;
        ent.MortalidadM     = dto.MortalidadM;
        ent.MortalidadH     = dto.MortalidadH;
        ent.SelM            = dto.SelM;
        ent.SelH            = dto.SelH;
        ent.ErrorM          = dto.ErrorM;
        ent.ErrorH          = dto.ErrorH;
        ent.TipoAlimento    = dto.TipoAlimento;
        ent.ConsumoAlimento = dto.ConsumoAlimento;
        ent.Observaciones   = dto.Observaciones;

        await _ctx.SaveChangesAsync();

        return await GetByIdAsync(ent.Id);
    }

    // ======================================================
    // DELETE (verifica pertenencia del Lote al tenant)
    // ======================================================
    public async Task<bool> DeleteAsync(int id)
    {
        var ent = await _ctx.LoteSeguimientos.FindAsync(id);
        if (ent is null) return false;

        var ok = await _ctx.Lotes.AsNoTracking()
            .AnyAsync(l => l.LoteId == ent.LoteId && l.CompanyId == _current.CompanyId);
        if (!ok) return false;

        _ctx.LoteSeguimientos.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }
}
