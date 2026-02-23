// Seguimiento diario por lote reproductora aves de engorde. Persiste en seguimiento_diario_lote_reproductora_aves_engorde.
// DTO reutiliza SeguimientoLoteLevanteDto con LoteId = lote_reproductora_ave_engorde_id.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoDiarioLoteReproductoraService : ISeguimientoDiarioLoteReproductoraService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;

    public SeguimientoDiarioLoteReproductoraService(ZooSanMarinoContext ctx, ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    private static SeguimientoLoteLevanteDto MapToDto(SeguimientoDiarioLoteReproductoraAvesEngorde e)
    {
        return new SeguimientoLoteLevanteDto(
            Id: (int)e.Id,
            LoteId: e.LoteReproductoraAveEngordeId,
            LotePosturaLevanteId: null,
            FechaRegistro: e.Fecha,
            MortalidadHembras: e.MortalidadHembras ?? 0,
            MortalidadMachos: e.MortalidadMachos ?? 0,
            SelH: e.SelH ?? 0,
            SelM: e.SelM ?? 0,
            ErrorSexajeHembras: e.ErrorSexajeHembras ?? 0,
            ErrorSexajeMachos: e.ErrorSexajeMachos ?? 0,
            ConsumoKgHembras: (double)(e.ConsumoKgHembras ?? 0),
            TipoAlimento: e.TipoAlimento ?? "",
            Observaciones: e.Observaciones,
            KcalAlH: e.KcalAlH,
            ProtAlH: e.ProtAlH,
            KcalAveH: e.KcalAveH,
            ProtAveH: e.ProtAveH,
            Ciclo: e.Ciclo ?? "Normal",
            ConsumoKgMachos: e.ConsumoKgMachos.HasValue ? (double)e.ConsumoKgMachos.Value : null,
            PesoPromH: e.PesoPromHembras,
            PesoPromM: e.PesoPromMachos,
            UniformidadH: e.UniformidadHembras,
            UniformidadM: e.UniformidadMachos,
            CvH: e.CvHembras,
            CvM: e.CvMachos,
            Metadata: e.Metadata,
            ItemsAdicionales: e.ItemsAdicionales,
            ConsumoAguaDiario: e.ConsumoAguaDiario,
            ConsumoAguaPh: e.ConsumoAguaPh,
            ConsumoAguaOrp: e.ConsumoAguaOrp,
            ConsumoAguaTemperatura: e.ConsumoAguaTemperatura,
            CreatedByUserId: e.CreatedByUserId
        );
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> GetByLoteReproductoraAsync(int loteReproductoraId)
    {
        var companyId = _current.CompanyId;
        var exists = await (from l in _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                           join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                           where l.Id == loteReproductoraId && lae.CompanyId == companyId && lae.DeletedAt == null
                           select 1).AnyAsync();
        if (!exists) return Array.Empty<SeguimientoLoteLevanteDto>();

        var list = await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
            .AsNoTracking()
            .Where(s => s.LoteReproductoraAveEngordeId == loteReproductoraId)
            .OrderBy(s => s.Fecha)
            .ToListAsync();
        return list.Select(MapToDto);
    }

    public async Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id)
    {
        var companyId = _current.CompanyId;
        var e = await (from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                       join l in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals l.Id
                       join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                       where s.Id == id && lae.CompanyId == companyId && lae.DeletedAt == null
                       select s).SingleOrDefaultAsync();
        return e is null ? null : MapToDto(e);
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteReproductoraId, DateTime? desde, DateTime? hasta)
    {
        var companyId = _current.CompanyId;
        var q = from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                join l in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals l.Id
                join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                where lae.CompanyId == companyId && lae.DeletedAt == null
                   && (!loteReproductoraId.HasValue || s.LoteReproductoraAveEngordeId == loteReproductoraId.Value)
                   && (!desde.HasValue || s.Fecha >= desde.Value)
                   && (!hasta.HasValue || s.Fecha <= hasta.Value)
                orderby s.Fecha
                select s;
        var list = await q.ToListAsync();
        return list.Select(MapToDto);
    }

    public async Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto)
    {
        var companyId = _current.CompanyId;
        var loteRep = await (from l in _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                             join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                             where l.Id == dto.LoteId && lae.CompanyId == companyId && lae.DeletedAt == null
                             select l).SingleOrDefaultAsync();
        if (loteRep is null)
            throw new InvalidOperationException($"Lote reproductora aves de engorde '{dto.LoteId}' no existe o no pertenece a la compañía.");

        var ent = new SeguimientoDiarioLoteReproductoraAvesEngorde
        {
            LoteReproductoraAveEngordeId = dto.LoteId,
            Fecha = dto.FechaRegistro,
            MortalidadHembras = dto.MortalidadHembras,
            MortalidadMachos = dto.MortalidadMachos,
            SelH = dto.SelH,
            SelM = dto.SelM,
            ErrorSexajeHembras = dto.ErrorSexajeHembras,
            ErrorSexajeMachos = dto.ErrorSexajeMachos,
            ConsumoKgHembras = (decimal)dto.ConsumoKgHembras,
            ConsumoKgMachos = dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null,
            TipoAlimento = dto.TipoAlimento,
            Observaciones = dto.Observaciones,
            Ciclo = dto.Ciclo,
            PesoPromHembras = dto.PesoPromH,
            PesoPromMachos = dto.PesoPromM,
            UniformidadHembras = dto.UniformidadH,
            UniformidadMachos = dto.UniformidadM,
            CvHembras = dto.CvH,
            CvMachos = dto.CvM,
            ConsumoAguaDiario = dto.ConsumoAguaDiario,
            ConsumoAguaPh = dto.ConsumoAguaPh,
            ConsumoAguaOrp = dto.ConsumoAguaOrp,
            ConsumoAguaTemperatura = dto.ConsumoAguaTemperatura,
            Metadata = dto.Metadata,
            ItemsAdicionales = dto.ItemsAdicionales,
            KcalAlH = dto.KcalAlH,
            ProtAlH = dto.ProtAlH,
            KcalAveH = dto.KcalAveH,
            ProtAveH = dto.ProtAveH,
            CreatedByUserId = dto.CreatedByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };
        _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.Add(ent);
        await _ctx.SaveChangesAsync();
        return MapToDto(ent);
    }

    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var companyId = _current.CompanyId;
        var ent = await (from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
                         join l in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals l.Id
                         join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                         where s.Id == dto.Id && lae.CompanyId == companyId && lae.DeletedAt == null
                         select s).SingleOrDefaultAsync();
        if (ent is null) return null;

        ent.Fecha = dto.FechaRegistro;
        ent.MortalidadHembras = dto.MortalidadHembras;
        ent.MortalidadMachos = dto.MortalidadMachos;
        ent.SelH = dto.SelH;
        ent.SelM = dto.SelM;
        ent.ErrorSexajeHembras = dto.ErrorSexajeHembras;
        ent.ErrorSexajeMachos = dto.ErrorSexajeMachos;
        ent.ConsumoKgHembras = (decimal)dto.ConsumoKgHembras;
        ent.ConsumoKgMachos = dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null;
        ent.TipoAlimento = dto.TipoAlimento;
        ent.Observaciones = dto.Observaciones;
        ent.Ciclo = dto.Ciclo;
        ent.PesoPromHembras = dto.PesoPromH;
        ent.PesoPromMachos = dto.PesoPromM;
        ent.UniformidadHembras = dto.UniformidadH;
        ent.UniformidadMachos = dto.UniformidadM;
        ent.CvHembras = dto.CvH;
        ent.CvMachos = dto.CvM;
        ent.ConsumoAguaDiario = dto.ConsumoAguaDiario;
        ent.ConsumoAguaPh = dto.ConsumoAguaPh;
        ent.ConsumoAguaOrp = dto.ConsumoAguaOrp;
        ent.ConsumoAguaTemperatura = dto.ConsumoAguaTemperatura;
        ent.Metadata = dto.Metadata;
        ent.ItemsAdicionales = dto.ItemsAdicionales;
        ent.KcalAlH = dto.KcalAlH;
        ent.ProtAlH = dto.ProtAlH;
        ent.KcalAveH = dto.KcalAveH;
        ent.ProtAveH = dto.ProtAveH;
        ent.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        return MapToDto(ent);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var companyId = _current.CompanyId;
        var ent = await (from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
                         join l in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals l.Id
                         join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                         where s.Id == id && lae.CompanyId == companyId && lae.DeletedAt == null
                         select s).SingleOrDefaultAsync();
        if (ent is null) return false;
        _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }
}
