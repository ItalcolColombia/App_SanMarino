// Helpers de inventario y recálculo de saldo de alimento del lote (Ecuador), portados de
// SeguimientoAvesEngordeService: devolución de aves, snapshot de stock por día, histórico de
// consumo por ítem y recálculo secuencial del saldo con piso 0.
// Partial de SeguimientoAvesEngordeEcuadorService.
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeEcuadorService
{
    private async Task DevolverAvesAlInventarioAsync(int loteId, int hembras, int machos)
    {
        if (hembras <= 0 && machos <= 0) return;
        var inv = await _ctx.InventarioAves
            .Where(i => i.LoteId == loteId
                && i.CompanyId == _current.CompanyId
                && i.DeletedAt == null
                && i.Estado == "Activo")
            .OrderByDescending(i => i.FechaActualizacion)
            .FirstOrDefaultAsync();
        if (inv == null) return;
        inv.CantidadHembras += Math.Max(0, hembras);
        inv.CantidadMachos += Math.Max(0, machos);
        inv.FechaActualizacion = DateTime.UtcNow;
        inv.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
    }

    private async Task<Dictionary<string, object?>> BuildStockMetadataPatchAsync(int loteId, DateTime fecha)
    {
        var day = fecha.Date;
        var companyId = _current.CompanyId;
        var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);

        var query = _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId
                && x.LoteAveEngordeId == loteId
                && x.FechaOperacion == day
                && !x.Anulado
                && !((x.Referencia != null && x.Referencia.Contains("devolución por eliminación"))
                     || (x.Referencia != null && x.Referencia.Contains("devolucion por eliminacion")))
                && !(x.TipoEvento == "INV_INGRESO"
                     && x.Referencia != null
                     && x.Referencia.StartsWith("Seguimiento aves engorde #"))
                && (x.TipoEvento == "INV_INGRESO"
                    || x.TipoEvento == "INV_TRASLADO_ENTRADA"
                    || x.TipoEvento == "VENTA_AVES"));

        if (fechaMinSeg.HasValue)
            query = query.Where(x => x.FechaOperacion >= fechaMinSeg.Value.Date);
        if (fechaMaxSeg.HasValue)
            query = query.Where(x => x.FechaOperacion <= fechaMaxSeg.Value.Date.AddDays(1).AddTicks(-1));

        var agg = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                IngresoKg = g.Sum(x => x.TipoEvento == "INV_INGRESO" ? (x.CantidadKg ?? 0m) : 0m),
                TrasladoKg = g.Sum(x => x.TipoEvento == "INV_TRASLADO_ENTRADA" ? (x.CantidadKg ?? 0m) : 0m),
                DespachoH = g.Sum(x => x.TipoEvento == "VENTA_AVES" ? (x.CantidadHembras ?? 0) : 0),
                DespachoM = g.Sum(x => x.TipoEvento == "VENTA_AVES" ? (x.CantidadMachos ?? 0) : 0),
                Documento = g
                    .Where(x => x.TipoEvento == "INV_INGRESO")
                    .Select(x => x.NumeroDocumento ?? x.Referencia)
                    .Max()
            })
            .SingleOrDefaultAsync();

        var patch = new Dictionary<string, object?>();
        if (agg is null) return patch;

        if (agg.IngresoKg > 0)
        {
            var s = FormatKg(agg.IngresoKg);
            patch["ingresoAlimento"] = s;
            patch["ingreso_alimento"] = s;
            patch["ingresoAlimentoKg"] = agg.IngresoKg;
        }
        if (agg.TrasladoKg > 0)
        {
            var s = FormatKg(agg.TrasladoKg);
            patch["traslado"] = s;
            patch["notaTraslado"] = s;
            patch["trasladoAlimento"] = s;
        }
        if (!string.IsNullOrWhiteSpace(agg.Documento))
        {
            var d = agg.Documento.Trim();
            patch["documento"] = d;
            patch["documentoAlimento"] = d;
            patch["nroDocumento"] = d;
            patch["numeroDocumento"] = d;
        }
        if (agg.DespachoH > 0)
        {
            patch["despachoHembras"] = agg.DespachoH;
            patch["despachoH"] = agg.DespachoH;
            patch["despacho_hembra"] = agg.DespachoH;
        }
        if (agg.DespachoM > 0)
        {
            patch["despachoMachos"] = agg.DespachoM;
            patch["despachoM"] = agg.DespachoM;
            patch["despacho_macho"] = agg.DespachoM;
        }
        return patch;
    }

    /// <summary>
    /// Construye el histórico de consumo de alimento por ítem para el campo historico_consumo_alimento.
    /// saldo_inicial = stock actual + oldConsumo (para edición, para restituir al estado pre-consumo del registro anterior).
    /// </summary>
    private async Task<JsonDocument?> BuildHistoricoConsumoAlimentoAsync(
        JsonDocument? metadata,
        int farmId, string? nucleoId, string? galponId,
        Dictionary<int, decimal>? oldByItemId = null)
    {
        if (metadata is null) return null;
        var newByItemId = ParseMetadataItemsToKg(metadata.RootElement);
        if (newByItemId.Count == 0) return null;

        var itemIds = newByItemId.Keys.ToList();
        var catalogItems = await _ctx.ItemInventario.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Nombre);

        var nucleoIdN = (nucleoId ?? "").Trim();
        var galponIdN = (galponId ?? "").Trim();
        var stockByItem = await _ctx.InventarioGestionStock.AsNoTracking()
            .Where(s =>
                s.FarmId == farmId
                && (s.NucleoId == null ? "" : s.NucleoId.Trim()) == nucleoIdN
                && (s.GalponId == null ? "" : s.GalponId.Trim()) == galponIdN
                && itemIds.Contains(s.ItemInventarioEcuadorId))
            .ToDictionaryAsync(s => s.ItemInventarioEcuadorId, s => s.Quantity);

        var historico = new List<object>();
        foreach (var kv in newByItemId)
        {
            var itemId = kv.Key;
            var consumo = kv.Value;
            var nombre = catalogItems.GetValueOrDefault(itemId, $"Ítem #{itemId}");
            var oldConsumo = oldByItemId?.GetValueOrDefault(itemId, 0m) ?? 0m;
            var currentStock = stockByItem.GetValueOrDefault(itemId, 0m);
            var saldoInicial = currentStock + oldConsumo;
            var saldoFinal = Math.Max(0, saldoInicial - consumo);
            historico.Add(new
            {
                nombre_alimento = nombre,
                saldo_inicial = saldoInicial,
                consumo = consumo,
                saldo_final = saldoFinal,
                unidad_medida = "kg"
            });
        }
        if (historico.Count == 0) return null;
        return JsonDocument.Parse(JsonSerializer.Serialize(historico));
    }

    // ─── Recálculo de saldo de alimento del lote ───────────────────────────────
    // Replica RecalcularSaldoAlimentoPorLoteAsync de SeguimientoAvesEngordeService.
    // No duplica INV_CONSUMO del histórico (ya descontado en seguimiento); aplica
    // piso 0 después de cada evento. La función SQL fn_seguimiento_diario_engorde
    // ahora calcula el saldo dinámicamente (fix #10), pero seguimos persistiendo
    // para consumidores que leen la columna directamente.

    private readonly record struct SaldoAlimentoEvent(string Ymd, int Ord, long Tie, long? SegId, decimal Delta);

    private static string FormatYmd(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static long TsSeguimiento(SeguimientoDiarioAvesEngorde s)
    {
        var t = new DateTimeOffset(s.Fecha.Year, s.Fecha.Month, s.Fecha.Day, 12, 0, 0, TimeSpan.Zero);
        return t.ToUnixTimeMilliseconds();
    }

    private static long TsHistorico(LoteRegistroHistoricoUnificado h) =>
        h.CreatedAt.ToUnixTimeMilliseconds();

    private static string? YmdHistoricoEfectivo(LoteRegistroHistoricoUnificado h)
        => FormatYmd(h.FechaOperacion);

    private static bool TryGetHistDeltaAndOrd(LoteRegistroHistoricoUnificado h, out decimal delta, out int ord)
        => SaldoAlimentoEngordeCalculos.TryGetHistDeltaAndOrd(h, out delta, out ord);

    /// <summary>
    /// ⚠️ FIX #12 (2026-05-28): si <paramref name="fechaEncaset"/> se proporciona, los movimientos
    /// anteriores al encaset se ignoran (galpón se considera "limpio"). Antes la apertura heredaba
    /// inventario residual del lote previo del mismo galpón.
    /// </summary>
    private static decimal ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
        IReadOnlyList<LoteRegistroHistoricoUnificado> hist,
        DateTime firstSegDate,
        DateTime? fechaEncaset = null)
    {
        var firstYmd = FormatYmd(firstSegDate.Date);
        var encasetYmd = fechaEncaset.HasValue ? FormatYmd(fechaEncaset.Value.Date) : null;
        var rows = new List<(string ymd, long ts, decimal delta)>();
        foreach (var h in hist)
        {
            var ymd = YmdHistoricoEfectivo(h);
            if (ymd is null || string.Compare(ymd, firstYmd, StringComparison.Ordinal) >= 0) continue;
            if (encasetYmd is not null && string.Compare(ymd, encasetYmd, StringComparison.Ordinal) < 0) continue;
            if (!TryGetHistDeltaAndOrd(h, out var d, out _)) continue;
            rows.Add((ymd, TsHistorico(h), d));
        }
        rows.Sort((a, b) =>
        {
            var c = string.Compare(a.ymd, b.ymd, StringComparison.Ordinal);
            if (c != 0) return c;
            return a.ts.CompareTo(b.ts);
        });
        decimal bal = 0;
        foreach (var r in rows)
        {
            bal += r.delta;
            if (bal < 0) bal = 0;
        }
        return bal;
    }

    private async Task RecalcularSaldoAlimentoPorLoteAsync(int loteId, int companyId, CancellationToken ct = default)
    {
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.FechaEncaset, l.GranjaId, l.NucleoId, l.GalponId })
            .FirstOrDefaultAsync(ct);
        if (lote is null) return;

        var farmId = lote.GranjaId;
        var nucleoId = (lote.NucleoId ?? "").Trim();
        var galponId = (lote.GalponId ?? "").Trim();

        var hist = await _ctx.LoteRegistroHistoricoUnificados.AsNoTracking()
            .Where(h =>
                h.CompanyId == companyId
                && !h.Anulado
                && h.TipoEvento != "VENTA_AVES"
                && !(h.TipoEvento == "INV_INGRESO"
                     && h.Referencia != null
                     && h.Referencia.StartsWith("Seguimiento aves engorde #"))
                && h.FarmId == farmId
                && (h.NucleoId == null ? "" : h.NucleoId.Trim()) == nucleoId
                && (h.GalponId == null ? "" : h.GalponId.Trim()) == galponId)
            .OrderBy(h => h.FechaOperacion)
            .ThenBy(h => h.Id)
            .ToListAsync(ct);

        var segs = await _ctx.SeguimientoDiarioAvesEngorde
            .Where(s => s.LoteAveEngordeId == loteId)
            .OrderBy(s => s.Fecha)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);
        if (segs.Count == 0) return;

        var firstSegDate = segs.Min(s => s.Fecha.Date);
        var encYmd = lote.FechaEncaset.HasValue ? FormatYmd(lote.FechaEncaset.Value.Date) : null;
        var firstYmd = FormatYmd(firstSegDate);
        var opening = ComputeSaldoAperturaGalponAntesPrimerSeguimiento(hist, firstSegDate, lote.FechaEncaset);

        var events = new List<SaldoAlimentoEvent>(hist.Count + segs.Count);
        foreach (var h in hist)
        {
            var ymd = YmdHistoricoEfectivo(h);
            if (ymd is null || string.Compare(ymd, firstYmd, StringComparison.Ordinal) < 0) continue;
            if (encYmd is not null && string.Compare(ymd, encYmd, StringComparison.Ordinal) < 0) continue;
            if (!TryGetHistDeltaAndOrd(h, out var delta, out var ord)) continue;
            events.Add(new SaldoAlimentoEvent(ymd, ord, TsHistorico(h), null, delta));
        }
        foreach (var s in segs)
        {
            var ymd = FormatYmd(s.Fecha.Date);
            var ch = s.ConsumoKgHembras ?? 0;
            var cm = s.ConsumoKgMachos ?? 0;
            events.Add(new SaldoAlimentoEvent(ymd, 3, TsSeguimiento(s), s.Id, -(ch + cm)));
        }
        events.Sort((a, b) =>
        {
            var c = string.Compare(a.Ymd, b.Ymd, StringComparison.Ordinal);
            if (c != 0) return c;
            if (a.Ord != b.Ord) return a.Ord.CompareTo(b.Ord);
            if (a.Tie != b.Tie) return a.Tie.CompareTo(b.Tie);
            return (a.SegId ?? 0L).CompareTo(b.SegId ?? 0L);
        });

        var saldoPorSegId = new Dictionary<long, decimal>();
        decimal bal = opening;
        foreach (var e in events)
        {
            bal += e.Delta;
            if (bal < 0) bal = 0;
            if (e.SegId.HasValue) saldoPorSegId[e.SegId.Value] = bal;
        }
        foreach (var s in segs)
            s.SaldoAlimentoKg = saldoPorSegId.TryGetValue(s.Id, out var sal) ? sal : bal;
        await _ctx.SaveChangesAsync(ct);
    }
}
