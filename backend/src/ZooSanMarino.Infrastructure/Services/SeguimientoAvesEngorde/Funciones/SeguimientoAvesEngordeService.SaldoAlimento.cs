// Recálculo y persistencia del saldo de alimento (kg) por registro de seguimiento del lote.
// Carga histórico + seguimientos (EF) y delega la reducción aritmética pura en
// SeguimientoAvesEngordeCalculos. Partial de SeguimientoAvesEngordeService.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeService
{
    /// <summary>
    /// Recalcula y persiste <see cref="Domain.Entities.SeguimientoDiarioAvesEngorde.SaldoAlimentoKg"/> para todos los registros diarios del lote.
    /// Misma lógica que el front (computeSaldoAlimentoKgPorSeguimiento): no duplica INV_CONSUMO del histórico,
    /// resta consumo del seguimiento, orden estable, piso en 0 tras cada paso.
    /// </summary>
    private async Task RecalcularSaldoAlimentoPorLoteAsync(int loteId, int companyId, CancellationToken ct = default)
    {
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.FechaEncaset, l.GranjaId, l.NucleoId, l.GalponId })
            .FirstOrDefaultAsync(ct);
        if (lote is null)
            return;

        // Usar scope de galpón (misma lógica que QueryHistoricoUnificadoDtosAsync y el frontend):
        // los movimientos de alimento (INV_INGRESO, INV_TRASLADO_*) se registran a nivel galpón
        // y pueden tener lote_ave_engorde_id nulo cuando el trigger corre antes de que exista el lote.
        var farmId     = lote.GranjaId;
        var nucleoId   = (lote.NucleoId ?? "").Trim();
        var galponId   = (lote.GalponId ?? "").Trim();

        var hist = await _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(h =>
                h.CompanyId == companyId
                && !h.Anulado
                && h.TipoEvento != "VENTA_AVES"
                // Excluir INV_INGRESO generados por el propio sistema de seguimiento
                // (devoluciones por eliminación y ajustes a la baja en edición).
                // Estos son asientos contables de reversión del inventario físico; el consumo
                // real ya queda capturado en los registros de seguimiento diario, y incluirlos
                // aquí inflaría el saldo de alimento calculado.
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

        if (segs.Count == 0)
            return;

        // El alimento vive en la bodega del GALPÓN, no del lote: el histórico de arriba ya se lee con
        // scope galpón, así que el consumo tiene que leerse igual o el saldo queda inflado en lo que
        // gastó el otro lote. Solo cuentan los lotes que CONVIVEN (rangos de seguimiento solapados):
        // un galpón que encadena ciclos sucesivos no comparte bodega, cada ciclo gasta lo suyo.
        // Espeja el CTE `consumo_galpon_por_fecha` de fn_seguimiento_diario_engorde (v10).
        var desde = segs[0].Fecha.Date;
        var hasta = segs[^1].Fecha.Date;

        var segsGalpon = await _ctx.SeguimientoDiarioAvesEngorde
            .AsNoTracking()   // solo aportan su consumo al saldo; los que se persisten son los de `segs`
            .Where(s => s.LoteAveEngordeId != loteId
                     && _ctx.LoteAveEngorde.Any(l2 =>
                            l2.LoteAveEngordeId == s.LoteAveEngordeId
                         && l2.CompanyId == companyId
                         && l2.DeletedAt == null
                         && l2.GranjaId == farmId
                         && (l2.NucleoId == null ? "" : l2.NucleoId.Trim()) == nucleoId
                         && (l2.GalponId == null ? "" : l2.GalponId.Trim()) == galponId)
                     && _ctx.SeguimientoDiarioAvesEngorde
                            .Where(s2 => s2.LoteAveEngordeId == s.LoteAveEngordeId)
                            .Min(s2 => s2.Fecha) <= hasta
                     && _ctx.SeguimientoDiarioAvesEngorde
                            .Where(s2 => s2.LoteAveEngordeId == s.LoteAveEngordeId)
                            .Max(s2 => s2.Fecha) >= desde)
            .ToListAsync(ct);

        // El cálculo consume la lista completa (propios + convivientes) para que el saldo por fecha
        // sea el del galpón; abajo solo se persisten los registros de ESTE lote.
        var segsParaSaldo = segsGalpon.Count == 0
            ? segs
            : segs.Concat(segsGalpon).OrderBy(s => s.Fecha).ThenBy(s => s.Id).ToList();

        // ⭐ v11: los lotes del galpón que NO conviven conmigo (ciclos sucesivos) son AJENOS: su
        // alimento no entra en mi apertura. Sin esto, la ventana previa al encaset se comía la
        // limpieza de cierre del ciclo anterior y la apertura salía negativa. Espeja el CTE
        // `lotes_ajenos` de fn_seguimiento_diario_engorde (v11).
        var ciclosGalpon = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l2 => l2.LoteAveEngordeId != loteId
                      && l2.CompanyId == companyId
                      && l2.DeletedAt == null
                      && l2.GranjaId == farmId
                      && (l2.NucleoId == null ? "" : l2.NucleoId.Trim()) == nucleoId
                      && (l2.GalponId == null ? "" : l2.GalponId.Trim()) == galponId)
            .Select(l2 => new
            {
                l2.LoteAveEngordeId,
                SegMin = _ctx.SeguimientoDiarioAvesEngorde
                    .Where(s2 => s2.LoteAveEngordeId == l2.LoteAveEngordeId).Min(s2 => (DateTime?)s2.Fecha),
                SegMax = _ctx.SeguimientoDiarioAvesEngorde
                    .Where(s2 => s2.LoteAveEngordeId == l2.LoteAveEngordeId).Max(s2 => (DateTime?)s2.Fecha)
            })
            .ToListAsync(ct);

        var lotesAjenos = SaldoAlimentoEngordeCalculos.ResolverLotesAjenos(
            ciclosGalpon.Where(c => c.LoteAveEngordeId.HasValue)
                        .Select(c => (c.LoteAveEngordeId!.Value, c.SegMin, c.SegMax)),
            desde, hasta);

        // Ventana previa al encaset configurada por la empresa: el preiniciador llega antes que los
        // pollitos y sin esto sus kilos quedaban fuera del saldo aunque estuvieran en el galpón.
        var diasPrevios = await _ctx.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => (int?)c.DiasAlimentoPrevioEncaset)
            .FirstOrDefaultAsync(ct);

        var (saldoPorSegId, bal) = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            hist, segsParaSaldo, lote.FechaEncaset, diasPrevios, lotesAjenos);

        foreach (var s in segs)
        {
            s.SaldoAlimentoKg = saldoPorSegId.TryGetValue(s.Id, out var sal) ? sal : bal;
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
