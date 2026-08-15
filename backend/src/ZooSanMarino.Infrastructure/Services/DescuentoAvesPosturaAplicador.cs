// src/ZooSanMarino.Infrastructure/Services/DescuentoAvesPosturaAplicador.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Aplica o revierte, sobre el saldo del lote de POSTURA, las aves que un seguimiento diario da de
/// baja (mortalidad + selección + error de sexaje).
///
/// <para>
/// El código vivía como dos métodos privados de <see cref="SeguimientoDiarioService"/> (que es donde
/// A7 lo centralizó para que cubriera alta, edición, borrado y merge sobre traslado). Se extrajo
/// <b>sin tocar la aritmética</b> —mismo cálculo puro, mismo clamp, mismo orden, mismos fallbacks de
/// inicialización— porque la doble validación necesita aplicar el descuento en un segundo momento: al
/// <b>validar</b>, no al guardar. Con los métodos privados, esa segunda vía habría tenido que
/// reescribir la cuenta, y tener el mismo número calculado en dos lugares es exactamente lo que este
/// repositorio ya pagó caro con el saldo de alimento.
/// </para>
///
/// <para>
/// Que sea estático y reciba el contexto es deliberado: no tiene estado propio y así lo pueden usar
/// tanto el service que lo llamaba como el de validación, sin sumar una dependencia de DI ni un ciclo.
/// </para>
/// </summary>
public static class DescuentoAvesPosturaAplicador
{
    /// <summary>
    /// Aplica (<paramref name="resta"/> = true) o revierte (false) el descuento sobre
    /// <c>lote_postura_levante</c>.
    ///
    /// <para>
    /// Resuelve el registro de levante por id y, si no llega o no existe, por <c>lote_id</c>: es el
    /// mismo doble camino de antes, porque hay flujos (carga masiva, PWA) que no traen el
    /// <c>LotePosturaLevanteId</c>. Si no encuentra ninguno no hace nada — no es un error: hay lotes
    /// sin fila de levante.
    /// </para>
    /// </summary>
    public static async Task AplicarLevanteAsync(
        ZooSanMarinoContext ctx,
        int? lplId, string loteIdStr,
        int mortH, int mortM, int selH, int selM, int errH, int errM,
        bool resta, CancellationToken ct = default)
    {
        // Cálculo puro compartido: mortalidad + selección + error de sexaje. Vive en
        // Application/Calculos con tests que fijan, entre otras cosas, la equivalencia entre
        // "revertir y reaplicar" y "aplicar el delta neto" — que es lo que hizo seguro mover esta
        // regla acá desde el módulo de levante (A7).
        var deltaH = DescuentoAvesSeguimientoCalculos.TotalDescuento(mortH, selH, errH);
        var deltaM = DescuentoAvesSeguimientoCalculos.TotalDescuento(mortM, selM, errM);
        if (deltaH == 0 && deltaM == 0) return;

        Domain.Entities.LotePosturaLevante? lpl = null;
        if (lplId.HasValue)
        {
            lpl = await ctx.LotePosturaLevante
                .Where(l => l.LotePosturaLevanteId == lplId.Value && l.DeletedAt == null)
                .FirstOrDefaultAsync(ct);
        }
        if (lpl == null && int.TryParse(loteIdStr, out var loteIdInt))
        {
            lpl = await ctx.LotePosturaLevante
                .Where(l => l.LoteId == loteIdInt && l.DeletedAt == null)
                .FirstOrDefaultAsync(ct);
        }
        if (lpl == null) return;

        var sign = resta ? -1 : 1;
        lpl.AvesHActual = DescuentoAvesSeguimientoCalculos.AplicarDelta(lpl.AvesHActual ?? 0, sign * deltaH);
        lpl.AvesMActual = DescuentoAvesSeguimientoCalculos.AplicarDelta(lpl.AvesMActual ?? 0, sign * deltaM);
        lpl.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Aplica o revierte el descuento sobre <c>lote_postura_produccion</c>.
    ///
    /// <para>
    /// Cuando <c>aves_h_actual</c>/<c>aves_m_actual</c> están en null se inicializan desde las
    /// iniciales de producción y, si tampoco están, desde las de levante. Ese orden de fallback se
    /// conserva tal cual: cambiarlo movería saldos históricos de lotes que nunca escribieron el
    /// actual.
    /// </para>
    /// </summary>
    public static async Task AplicarProduccionAsync(
        ZooSanMarinoContext ctx,
        int lppId,
        int mortH, int mortM, int selH, int selM, int errH, int errM,
        bool resta, CancellationToken ct = default)
    {
        var deltaH = DescuentoAvesSeguimientoCalculos.TotalDescuento(mortH, selH, errH);
        var deltaM = DescuentoAvesSeguimientoCalculos.TotalDescuento(mortM, selM, errM);
        if (deltaH == 0 && deltaM == 0) return;

        var lpp = await ctx.LotePosturaProduccion.FindAsync(new object[] { lppId }, ct);
        if (lpp == null) return;

        // Inicializar aves actuales si son null (usar iniciales)
        var avesH = lpp.AvesHActual ?? lpp.HembrasInicialesProd ?? lpp.AvesHInicial ?? 0;
        var avesM = lpp.AvesMActual ?? lpp.MachosInicialesProd ?? lpp.AvesMInicial ?? 0;

        var sign = resta ? -1 : 1;
        lpp.AvesHActual = Math.Max(0, avesH + sign * deltaH);
        lpp.AvesMActual = Math.Max(0, avesM + sign * deltaM);
        lpp.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync(ct);
    }
}
