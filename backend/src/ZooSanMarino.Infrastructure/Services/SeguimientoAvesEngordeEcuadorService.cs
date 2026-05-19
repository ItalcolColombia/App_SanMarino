using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoAvesEngordeEcuadorService : ISeguimientoAvesEngordeEcuadorService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;

    public SeguimientoAvesEngordeEcuadorService(ZooSanMarinoContext ctx, ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    public async Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id)
    {
        var entity = await _ctx.SeguimientoDiarioAvesEngordeEcuador.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteId, DateTime? desde, DateTime? hasta)
    {
        var query = _ctx.SeguimientoDiarioAvesEngordeEcuador.AsNoTracking();
        if (loteId.HasValue) query = query.Where(x => x.LoteAveEngordeId == loteId.Value);
        if (desde.HasValue) query = query.Where(x => x.Fecha >= desde.Value);
        if (hasta.HasValue) query = query.Where(x => x.Fecha <= hasta.Value);
        var entities = await query.OrderBy(x => x.Fecha).ToListAsync();
        return entities.Select(MapToDto);
    }

    public async Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto)
    {
        var entity = new SeguimientoDiarioAvesEngordeEcuador
        {
            LoteAveEngordeId = dto.LoteId,
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
            CreatedByUserId = _current?.UserId.ToString(),
            CreatedAt = DateTime.UtcNow,
            SaldoAlimentoKg = dto.SaldoAlimentoKg.HasValue ? (decimal)dto.SaldoAlimentoKg.Value : null,
            HistoricoConsumoAlimento = dto.HistoricoConsumoAlimento
        };
        _ctx.SeguimientoDiarioAvesEngordeEcuador.Add(entity);
        await _ctx.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var entity = await _ctx.SeguimientoDiarioAvesEngordeEcuador
            .FirstOrDefaultAsync(x => x.Id == dto.Id);
        if (entity is null) return null;

        entity.Fecha = dto.FechaRegistro;
        entity.MortalidadHembras = dto.MortalidadHembras;
        entity.MortalidadMachos = dto.MortalidadMachos;
        entity.SelH = dto.SelH;
        entity.SelM = dto.SelM;
        entity.ErrorSexajeHembras = dto.ErrorSexajeHembras;
        entity.ErrorSexajeMachos = dto.ErrorSexajeMachos;
        entity.ConsumoKgHembras = (decimal)dto.ConsumoKgHembras;
        entity.ConsumoKgMachos = dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null;
        entity.TipoAlimento = dto.TipoAlimento;
        entity.Observaciones = dto.Observaciones;
        entity.Ciclo = dto.Ciclo;
        entity.PesoPromHembras = dto.PesoPromH;
        entity.PesoPromMachos = dto.PesoPromM;
        entity.UniformidadHembras = dto.UniformidadH;
        entity.UniformidadMachos = dto.UniformidadM;
        entity.CvHembras = dto.CvH;
        entity.CvMachos = dto.CvM;
        entity.ConsumoAguaDiario = dto.ConsumoAguaDiario;
        entity.ConsumoAguaPh = dto.ConsumoAguaPh;
        entity.ConsumoAguaOrp = dto.ConsumoAguaOrp;
        entity.ConsumoAguaTemperatura = dto.ConsumoAguaTemperatura;
        entity.Metadata = dto.Metadata;
        entity.ItemsAdicionales = dto.ItemsAdicionales;
        entity.KcalAlH = dto.KcalAlH;
        entity.ProtAlH = dto.ProtAlH;
        entity.KcalAveH = dto.KcalAveH;
        entity.ProtAveH = dto.ProtAveH;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.SaldoAlimentoKg = dto.SaldoAlimentoKg.HasValue ? (decimal)dto.SaldoAlimentoKg.Value : null;
        entity.HistoricoConsumoAlimento = dto.HistoricoConsumoAlimento;

        _ctx.SeguimientoDiarioAvesEngordeEcuador.Update(entity);
        await _ctx.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _ctx.SeguimientoDiarioAvesEngordeEcuador.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return false;
        _ctx.SeguimientoDiarioAvesEngordeEcuador.Remove(entity);
        await _ctx.SaveChangesAsync();
        return true;
    }

    private static SeguimientoLoteLevanteDto MapToDto(SeguimientoDiarioAvesEngordeEcuador e)
    {
        return new SeguimientoLoteLevanteDto(
            Id: (int)e.Id,
            LoteId: e.LoteAveEngordeId,
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
            CreatedByUserId: e.CreatedByUserId,
            SaldoAlimentoKg: e.SaldoAlimentoKg.HasValue ? (double)e.SaldoAlimentoKg.Value : null,
            HistoricoConsumoAlimento: e.HistoricoConsumoAlimento
        );
    }
}
