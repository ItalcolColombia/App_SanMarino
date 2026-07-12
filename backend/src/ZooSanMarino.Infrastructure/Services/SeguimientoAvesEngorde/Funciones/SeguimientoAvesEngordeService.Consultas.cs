// Consultas de solo lectura del seguimiento diario de aves de engorde: listado por lote,
// historial unificado, liquidación, filtros y resultado. Partial de SeguimientoAvesEngordeService.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeService
{
    public async Task<SeguimientoAvesEngordePorLoteResponseDto> GetByLoteAsync(int loteId)
    {
        var companyId = _current.CompanyId;
        var exists = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (!exists)
            return new SeguimientoAvesEngordePorLoteResponseDto(
                Array.Empty<SeguimientoLoteLevanteDto>(),
                Array.Empty<LoteRegistroHistoricoUnificadoDto>());

        await RecalcularSaldoAlimentoPorLoteAsync(loteId, companyId);

        var list = await _ctx.SeguimientoDiarioAvesEngorde
            .AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteId)
            .OrderBy(s => s.Fecha)
            .ToListAsync();
        foreach (var s in list)
            SanitizeContaminatedDocumentMetadata(s);
        var seguimientos = list.Select(MapToDto).ToList();

        var historico = await QueryHistoricoUnificadoDtosAsync(loteId, companyId);

        return new SeguimientoAvesEngordePorLoteResponseDto(seguimientos, historico);
    }

    public async Task<IEnumerable<LoteRegistroHistoricoUnificadoDto>> GetHistoricoUnificadoPorLoteAsync(int loteId)
    {
        var companyId = _current.CompanyId;
        var exists = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (!exists) return Array.Empty<LoteRegistroHistoricoUnificadoDto>();

        return await QueryHistoricoUnificadoDtosAsync(loteId, companyId);
    }

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

        await RecalcularSaldoAlimentoPorLoteAsync(loteId, companyId);

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

        // Encaset / inicio real: mismo criterio que historial_lote_pollo_engorde (Inicio al crear el lote).
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

        // Aves vivas actuales: total inicio - (bajas acumuladas del seguimiento) - (ventas acumuladas)
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

    private async Task<IReadOnlyList<LoteRegistroHistoricoUnificadoDto>> QueryHistoricoUnificadoDtosAsync(int loteId, int companyId)
    {
        // Resolve the lote's physical location (granja / nucleo / galpon).
        // This is the source of truth used to filter by event type:
        //   - VENTA_AVES       → lote level  (lote_ave_engorde_id)
        //   - food movements   → galpon level (farm_id + nucleo_id + galpon_id)
        //     (food is received at galpon level; lote_ave_engorde_id may be NULL if the
        //      trigger ran before the lote was created — this covers that case too)
        var loteInfo = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId })
            .SingleOrDefaultAsync();

        if (loteInfo is null)
            return Array.Empty<LoteRegistroHistoricoUnificadoDto>();

        int farmId      = loteInfo.GranjaId;
        string nucleoId = (loteInfo.NucleoId ?? "").Trim();
        string galponId = (loteInfo.GalponId ?? "").Trim();

        // Calcular rango de fechas del ciclo de vida del lote:
        // Límite inferior: min(fecha de seguimiento) — Límite superior: max(fecha de seguimiento)
        // Esto aísla el histórico del lote actual de registros de lotes previos en el mismo galpón.
        var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);

        var query = _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(h => h.CompanyId == companyId
                && !h.Anulado
                && !((h.Referencia != null && h.Referencia.Contains("devolución por eliminación"))
                     || (h.Referencia != null && h.Referencia.Contains("devolucion por eliminacion")))
                // Excluir INV_INGRESO generados por el sistema de seguimiento (devoluciones
                // por edición a la baja). Estos ingresos son reversiones contables del
                // inventario físico y no deben mostrarse como "ingreso de alimento" en la
                // tabla diaria; su ausencia del filtro haría que ingresoKg apareciera inflado.
                && !(h.TipoEvento == "INV_INGRESO"
                     && h.Referencia != null
                     && h.Referencia.StartsWith("Seguimiento aves engorde #"))
                && (
                    // Bird sales: scoped to the specific lote
                    (h.TipoEvento == "VENTA_AVES" && h.LoteAveEngordeId == loteId)
                    ||
                    // Food movements: scoped to the galpon regardless of lote assignment
                    (h.TipoEvento != "VENTA_AVES"
                        && h.FarmId == farmId
                        && (h.NucleoId == null ? "" : h.NucleoId.Trim()) == nucleoId
                        && (h.GalponId == null ? "" : h.GalponId.Trim()) == galponId)
                ));

        // Aplicar filtro de rango de fechas (ciclo de vida del lote)
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

    public async Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id)
    {
        var companyId = _current.CompanyId;
        var e = await (from s in _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                       join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                       where s.Id == id && l.CompanyId == companyId && l.DeletedAt == null
                       select s).SingleOrDefaultAsync();
        return e is null ? null : MapToDto(e);
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteId, DateTime? desde, DateTime? hasta)
    {
        var companyId = _current.CompanyId;
        var q = from s in _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                where l.CompanyId == companyId && l.DeletedAt == null
                   && (!loteId.HasValue || s.LoteAveEngordeId == loteId.Value)
                   && (!desde.HasValue || s.Fecha >= desde.Value)
                   && (!hasta.HasValue || s.Fecha <= hasta.Value)
                orderby s.Fecha
                select s;
        var list = await q.ToListAsync();
        return list.Select(MapToDto);
    }

    public async Task<ResultadoLevanteResponse> GetResultadoAsync(int loteId, DateTime? desde, DateTime? hasta, bool recalcular = true)
    {
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote aves de engorde '{loteId}' no existe o no pertenece a la compañía.");

        return await Task.FromResult(new ResultadoLevanteResponse(loteId, desde?.Date, hasta?.Date, 0, new List<ResultadoLevanteItemDto>()));
    }
}
