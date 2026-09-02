// Helpers de inventario y recálculo de saldo de alimento del lote (Ecuador), portados de
// SeguimientoAvesEngordeService: snapshot de stock por día, histórico de consumo por ítem y
// recálculo secuencial del saldo con piso 0. El descuento/devolución de AVES ya no vive acá:
// lo resuelve RetiroAvesEngordeAplicador contra el maestro lote_ave_engorde.
// Partial de SeguimientoDiarioEngordeService.
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoDiarioEngordeService
{
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
    // ─── Recálculo del saldo de alimento del lote ─────────────────────────────
    //
    // UNA SOLA IMPLEMENTACIÓN (jul-2026)
    // Este archivo tenía su propia aritmética del saldo, distinta de la de
    // SeguimientoAvesEngordeService y de la de `fn_seguimiento_diario_engorde` en tres puntos: el
    // corte de la ventana previa al encaset, el piso en 0 y la exclusión del ciclo anterior. Esa
    // divergencia fue la causa directa de que el dato guardado y la pantalla mostraran números
    // distintos — Kilometro 22 / G0036: 11.380 kg guardados contra 3.420 en pantalla.
    //
    // Ahora delega en SaldoAlimentoEngordeAplicador, que escribe la columna DESDE la fn. Se borraron
    // los helpers que solo servían a la vieja aritmética (SaldoAlimentoEvent, FormatYmd,
    // TsSeguimiento, TsHistorico, YmdHistoricoEfectivo, TryGetHistDeltaAndOrd y
    // ComputeSaldoAperturaGalponAntesPrimerSeguimiento).
    //
    // La fórmula en C# sigue existiendo como ESPECIFICACIÓN EJECUTABLE en
    // SeguimientoAvesEngordeCalculos: sus tests son el contrato que la fn tiene que cumplir.

    /// <summary>
    /// Recalcula y persiste <see cref="Domain.Entities.SeguimientoDiarioAvesEngorde.SaldoAlimentoKg"/>
    /// de todos los registros diarios del lote, tomando el valor de
    /// <c>fn_seguimiento_diario_engorde</c> — la misma fuente que pinta la tabla diaria.
    /// <para>
    /// Escribe por SQL, no por entidades rastreadas. Los llamadores ya hacen
    /// <c>Entry(ent).ReloadAsync()</c> antes de mapear la respuesta, así que el DTO sale actualizado.
    /// </para>
    /// </summary>
    private async Task RecalcularSaldoAlimentoPorLoteAsync(int loteId, int companyId, CancellationToken ct = default)
    {
        // Se conserva el alcance por empresa: un lote de otra empresa no se toca.
        var propio = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteId
                        && l.CompanyId == companyId
                        && l.DeletedAt == null, ct);
        if (!propio)
            return;

        await SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync(_ctx, loteId, ct);
    }
}
