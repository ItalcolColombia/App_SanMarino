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

    // ─── Consultas estándar ───────────────────────────────────────────────────

    public async Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id)
    {
        var entity = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<SeguimientoAvesEngordePorLoteResponseDto> GetByLoteAsync(int loteId)
    {
        var companyId = _current.CompanyId;

        var exists = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (!exists)
            return new SeguimientoAvesEngordePorLoteResponseDto(
                Array.Empty<SeguimientoLoteLevanteDto>(),
                Array.Empty<LoteRegistroHistoricoUnificadoDto>());

        var list = await _ctx.SeguimientoDiarioAvesEngorde
            .AsNoTracking()
            .Where(x => x.LoteAveEngordeId == loteId)
            .OrderBy(x => x.Fecha)
            .ToListAsync();

        var seguimientos = list.Select(MapToDto).ToList();
        var historico    = await QueryHistoricoUnificadoDtosAsync(loteId, companyId);

        return new SeguimientoAvesEngordePorLoteResponseDto(seguimientos, historico);
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(
        int? loteId, DateTime? desde, DateTime? hasta)
    {
        var query = _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking();
        if (loteId.HasValue) query = query.Where(x => x.LoteAveEngordeId == loteId.Value);
        if (desde.HasValue)  query = query.Where(x => x.Fecha >= desde.Value);
        if (hasta.HasValue)  query = query.Where(x => x.Fecha <= hasta.Value);
        var entities = await query.OrderBy(x => x.Fecha).ToListAsync();
        return entities.Select(MapToDto);
    }

    // ─── Resumen liquidación ─────────────────────────────────────────────────

    public async Task<LiquidacionLoteEngordeResumenDto?> GetLiquidacionResumenAsync(int loteId)
    {
        var companyId = _current.CompanyId;
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new
            {
                l.LoteAveEngordeId,
                l.LoteNombre,
                l.EstadoOperativoLote,
                l.HembrasL,
                l.MachosL,
                l.Mixtas,
                l.AvesEncasetadas
            })
            .SingleOrDefaultAsync();
        if (lote is null) return null;

        var saldo = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteId)
            .OrderByDescending(s => s.Fecha)
            .Select(s => s.SaldoAlimentoKg)
            .FirstOrDefaultAsync();

        var ventas = await _ctx.LoteRegistroHistoricoUnificados.AsNoTracking()
            .Where(h => h.LoteAveEngordeId == loteId && h.CompanyId == companyId && !h.Anulado && h.TipoEvento == "VENTA_AVES")
            .ToListAsync();

        var vh = ventas.Sum(v => v.CantidadHembras ?? 0);
        var vm = ventas.Sum(v => v.CantidadMachos ?? 0);
        var vx = ventas.Sum(v => v.CantidadMixtas ?? 0);

        var ini = await _ctx.HistorialLotePolloEngorde.AsNoTracking()
            .Where(h =>
                h.CompanyId == companyId &&
                h.LoteAveEngordeId == loteId &&
                h.TipoLote == "LoteAveEngorde" &&
                h.TipoRegistro == "Inicio")
            .OrderBy(h => h.Id)
            .FirstOrDefaultAsync();

        int hInicio, mInicio, xInicio;
        if (ini != null)
        {
            hInicio = ini.AvesHembras;
            mInicio = ini.AvesMachos;
            xInicio = ini.AvesMixtas;
        }
        else
        {
            hInicio = lote.HembrasL ?? 0;
            mInicio = lote.MachosL ?? 0;
            xInicio = lote.Mixtas ?? 0;
            if (hInicio + mInicio + xInicio == 0 && lote.AvesEncasetadas.HasValue && lote.AvesEncasetadas.Value > 0)
                xInicio = lote.AvesEncasetadas.Value;
        }

        var totalInicio = hInicio + mInicio + xInicio;

        var bajas = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteId)
            .Select(s =>
                (s.MortalidadHembras ?? 0) +
                (s.MortalidadMachos ?? 0) +
                (s.SelH ?? 0) +
                (s.SelM ?? 0) +
                (s.ErrorSexajeHembras ?? 0) +
                (s.ErrorSexajeMachos ?? 0))
            .SumAsync();
        var avesVivas = Math.Max(0, totalInicio - bajas - (vh + vm + vx));

        return new LiquidacionLoteEngordeResumenDto(
            lote.LoteAveEngordeId ?? loteId,
            lote.LoteNombre ?? "",
            lote.EstadoOperativoLote ?? "Abierto",
            hInicio,
            mInicio,
            xInicio,
            totalInicio,
            vh,
            vm,
            vx,
            avesVivas,
            ventas.Count,
            saldo);
    }

    // ─── Tabla diaria via función SQL ────────────────────────────────────────

    public async Task<IReadOnlyList<SeguimientoDiarioTablaFilaDto>> GetTablaDiariaAsync(int loteId)
    {
        return await _ctx.Database
            .SqlQueryRaw<SeguimientoDiarioTablaFilaDto>(
                "SELECT * FROM fn_seguimiento_diario_engorde({0}::int)", loteId)
            .ToListAsync();
    }

    // ─── CRUD ────────────────────────────────────────────────────────────────

    public async Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto)
    {
        var entity = new SeguimientoDiarioAvesEngorde
        {
            LoteAveEngordeId       = dto.LoteId,
            Fecha                  = dto.FechaRegistro,
            MortalidadHembras      = dto.MortalidadHembras,
            MortalidadMachos       = dto.MortalidadMachos,
            SelH                   = dto.SelH,
            SelM                   = dto.SelM,
            ErrorSexajeHembras     = dto.ErrorSexajeHembras,
            ErrorSexajeMachos      = dto.ErrorSexajeMachos,
            ConsumoKgHembras       = (decimal)dto.ConsumoKgHembras,
            ConsumoKgMachos        = dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null,
            TipoAlimento           = dto.TipoAlimento,
            Observaciones          = dto.Observaciones,
            Ciclo                  = dto.Ciclo,
            PesoPromHembras        = dto.PesoPromH,
            PesoPromMachos         = dto.PesoPromM,
            UniformidadHembras     = dto.UniformidadH,
            UniformidadMachos      = dto.UniformidadM,
            CvHembras              = dto.CvH,
            CvMachos               = dto.CvM,
            ConsumoAguaDiario      = dto.ConsumoAguaDiario,
            ConsumoAguaPh          = dto.ConsumoAguaPh,
            ConsumoAguaOrp         = dto.ConsumoAguaOrp,
            ConsumoAguaTemperatura = dto.ConsumoAguaTemperatura,
            Metadata               = dto.Metadata,
            ItemsAdicionales       = dto.ItemsAdicionales,
            KcalAlH                = dto.KcalAlH,
            ProtAlH                = dto.ProtAlH,
            KcalAveH               = dto.KcalAveH,
            ProtAveH               = dto.ProtAveH,
            CreatedByUserId        = _current?.UserId.ToString(),
            CreatedAt              = DateTime.UtcNow,
            SaldoAlimentoKg        = dto.SaldoAlimentoKg.HasValue ? (decimal)dto.SaldoAlimentoKg.Value : null,
            HistoricoConsumoAlimento = dto.HistoricoConsumoAlimento
        };
        _ctx.SeguimientoDiarioAvesEngorde.Add(entity);
        await _ctx.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var entity = await _ctx.SeguimientoDiarioAvesEngorde
            .FirstOrDefaultAsync(x => x.Id == dto.Id);
        if (entity is null) return null;

        entity.Fecha                  = dto.FechaRegistro;
        entity.MortalidadHembras      = dto.MortalidadHembras;
        entity.MortalidadMachos       = dto.MortalidadMachos;
        entity.SelH                   = dto.SelH;
        entity.SelM                   = dto.SelM;
        entity.ErrorSexajeHembras     = dto.ErrorSexajeHembras;
        entity.ErrorSexajeMachos      = dto.ErrorSexajeMachos;
        entity.ConsumoKgHembras       = (decimal)dto.ConsumoKgHembras;
        entity.ConsumoKgMachos        = dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null;
        entity.TipoAlimento           = dto.TipoAlimento;
        entity.Observaciones          = dto.Observaciones;
        entity.Ciclo                  = dto.Ciclo;
        entity.PesoPromHembras        = dto.PesoPromH;
        entity.PesoPromMachos         = dto.PesoPromM;
        entity.UniformidadHembras     = dto.UniformidadH;
        entity.UniformidadMachos      = dto.UniformidadM;
        entity.CvHembras              = dto.CvH;
        entity.CvMachos               = dto.CvM;
        entity.ConsumoAguaDiario      = dto.ConsumoAguaDiario;
        entity.ConsumoAguaPh          = dto.ConsumoAguaPh;
        entity.ConsumoAguaOrp         = dto.ConsumoAguaOrp;
        entity.ConsumoAguaTemperatura = dto.ConsumoAguaTemperatura;
        entity.Metadata               = dto.Metadata;
        entity.ItemsAdicionales       = dto.ItemsAdicionales;
        entity.KcalAlH                = dto.KcalAlH;
        entity.ProtAlH                = dto.ProtAlH;
        entity.KcalAveH               = dto.KcalAveH;
        entity.ProtAveH               = dto.ProtAveH;
        entity.UpdatedAt              = DateTime.UtcNow;
        entity.SaldoAlimentoKg        = dto.SaldoAlimentoKg.HasValue ? (decimal)dto.SaldoAlimentoKg.Value : null;
        entity.HistoricoConsumoAlimento = dto.HistoricoConsumoAlimento;

        await _ctx.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _ctx.SeguimientoDiarioAvesEngorde.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return false;
        _ctx.SeguimientoDiarioAvesEngorde.Remove(entity);
        await _ctx.SaveChangesAsync();
        return true;
    }

    // ─── Histórico unificado ─────────────────────────────────────────────────

    private async Task<IReadOnlyList<LoteRegistroHistoricoUnificadoDto>> QueryHistoricoUnificadoDtosAsync(
        int loteId, int companyId)
    {
        var loteInfo = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId })
            .SingleOrDefaultAsync();

        if (loteInfo is null)
            return Array.Empty<LoteRegistroHistoricoUnificadoDto>();

        int    farmId   = loteInfo.GranjaId;
        string nucleoId = (loteInfo.NucleoId ?? "").Trim();
        string galponId = (loteInfo.GalponId ?? "").Trim();

        var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);

        var query = _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(h => h.CompanyId == companyId
                && !h.Anulado
                && !((h.Referencia != null && h.Referencia.Contains("devolución por eliminación"))
                     || (h.Referencia != null && h.Referencia.Contains("devolucion por eliminacion")))
                && !(h.TipoEvento == "INV_INGRESO"
                     && h.Referencia != null
                     && h.Referencia.StartsWith("Seguimiento aves engorde #"))
                && (
                    (h.TipoEvento == "VENTA_AVES" && h.LoteAveEngordeId == loteId)
                    ||
                    (h.TipoEvento != "VENTA_AVES"
                        && h.FarmId == farmId
                        && (h.NucleoId == null ? "" : h.NucleoId.Trim()) == nucleoId
                        && (h.GalponId == null ? "" : h.GalponId.Trim()) == galponId)
                ));

        if (fechaMinSeg.HasValue)
            query = query.Where(h => h.FechaOperacion >= fechaMinSeg.Value.Date);
        if (fechaMaxSeg.HasValue)
            query = query.Where(h => h.FechaOperacion <= fechaMaxSeg.Value.Date.AddDays(1).AddTicks(-1));

        var rows = await query
            .OrderBy(h => h.FechaOperacion)
            .ThenBy(h => h.Id)
            .ToListAsync();

        return rows.Select(MapHistoricoUnificado).ToList();
    }

    private async Task<(DateTime?, DateTime?)> CalcularRangoFechasLoteAsync(int loteId)
    {
        var segFechas = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteId)
            .Select(s => s.Fecha)
            .ToListAsync();

        return segFechas.Count == 0
            ? (null, null)
            : (segFechas.Min(), segFechas.Max());
    }

    // ─── Mappers ─────────────────────────────────────────────────────────────

    private static LoteRegistroHistoricoUnificadoDto MapHistoricoUnificado(LoteRegistroHistoricoUnificado e) =>
        new(
            Id: e.Id,
            CompanyId: e.CompanyId,
            LoteAveEngordeId: e.LoteAveEngordeId,
            FarmId: e.FarmId,
            NucleoId: e.NucleoId,
            GalponId: e.GalponId,
            FechaOperacion: e.FechaOperacion,
            TipoEvento: e.TipoEvento,
            OrigenTabla: e.OrigenTabla,
            OrigenId: e.OrigenId,
            MovementTypeOriginal: e.MovementTypeOriginal,
            ItemInventarioEcuadorId: e.ItemInventarioEcuadorId,
            ItemResumen: e.ItemResumen,
            CantidadKg: e.CantidadKg,
            Unidad: e.Unidad,
            CantidadHembras: e.CantidadHembras,
            CantidadMachos: e.CantidadMachos,
            CantidadMixtas: e.CantidadMixtas,
            Referencia: e.Referencia,
            NumeroDocumento: e.NumeroDocumento,
            AcumuladoEntradasAlimentoKg: e.AcumuladoEntradasAlimentoKg,
            Anulado: e.Anulado,
            CreatedAt: e.CreatedAt);

    private static SeguimientoLoteLevanteDto MapToDto(SeguimientoDiarioAvesEngorde e) =>
        new(
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
            HistoricoConsumoAlimento: e.HistoricoConsumoAlimento);
}
