// Consultas de solo lectura del seguimiento diario de aves engorde Ecuador: por id, por lote,
// filtrado, resumen de liquidación, tabla diaria (función SQL) e histórico unificado.
// Partial de SeguimientoAvesEngordeEcuadorService.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeEcuadorService
{
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
                l.AvesEncasetadas,
                l.MermaUnidades,
                l.MermaKilos
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

        var (hInicio, mInicio, xInicio) = LiquidacionEngordeCalculos.CalcularAvesInicio(
            ini != null, ini?.AvesHembras ?? 0, ini?.AvesMachos ?? 0, ini?.AvesMixtas ?? 0,
            lote.HembrasL, lote.MachosL, lote.Mixtas, lote.AvesEncasetadas);

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
        var avesVivas = LiquidacionEngordeCalculos.CalcularAvesVivas(totalInicio, bajas, vh + vm + vx);

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
            saldo,
            lote.MermaUnidades,
            lote.MermaKilos);
    }

    // ─── Tabla diaria via función SQL ────────────────────────────────────────

    public async Task<IReadOnlyList<SeguimientoDiarioTablaFilaDto>> GetTablaDiariaAsync(int loteId)
    {
        return await _ctx.Database
            .SqlQueryRaw<SeguimientoDiarioTablaFilaDto>(
                "SELECT * FROM fn_seguimiento_diario_engorde({0}::int)", loteId)
            .ToListAsync();
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
}
