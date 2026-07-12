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

        var (saldoPorSegId, bal) = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            hist, segs, lote.FechaEncaset);

        foreach (var s in segs)
        {
            s.SaldoAlimentoKg = saldoPorSegId.TryGetValue(s.Id, out var sal) ? sal : bal;
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
