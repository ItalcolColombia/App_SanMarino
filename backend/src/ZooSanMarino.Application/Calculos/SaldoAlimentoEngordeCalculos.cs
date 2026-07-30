// Cálculo puro compartido del saldo de alimento de engorde (multi-país).
// NOTA: YmdHistoricoEfectivo NO se comparte todavía: Colombia extrae la fecha
// efectiva desde la referencia del evento ("Seguimiento aves engorde #N yyyy-MM-dd",
// o cualquier fecha para INV_CONSUMO) y Ecuador usa FechaOperacion a secas.
// Esa divergencia afecta el orden del recálculo y está pendiente de decisión
// (ver tracker_estado.md — posible causa del descuadre del liquidador Ecuador).
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

public static class SaldoAlimentoEngordeCalculos
{
    /// <summary>
    /// Delta de kg y orden intra-día de un evento del histórico unificado para el
    /// recálculo de saldo de alimento. Solo INV_INGRESO (+, ord 0),
    /// INV_TRASLADO_ENTRADA (+, ord 1) e INV_TRASLADO_SALIDA (−, ord 2) participan.
    /// </summary>
    public static bool TryGetHistDeltaAndOrd(LoteRegistroHistoricoUnificado h, out decimal delta, out int ord)
    {
        delta = 0;
        ord = 0;
        if (h.Anulado) return false;
        var kg = h.CantidadKg ?? 0;
        switch (h.TipoEvento)
        {
            case "INV_INGRESO":
                if (kg == 0) return false;
                delta = kg; ord = 0; return true;
            case "INV_TRASLADO_ENTRADA":
                if (kg == 0) return false;
                delta = kg; ord = 1; return true;
            case "INV_TRASLADO_SALIDA":
                if (kg == 0) return false;
                delta = -Math.Abs(kg); ord = 2; return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// ¿El movimiento pertenece a un ciclo AJENO del mismo galpón? (v11, 2026-07-29)
    /// <para>
    /// «Ajeno» = otro lote del galpón cuyo rango de seguimiento NO se solapa con el del lote
    /// consultado. Es el complemento exacto del criterio «CONVIVEN» que ya se usa para repartir el
    /// consumo: si a un lote no le cuento el consumo porque no comparte bodega conmigo, tampoco
    /// puedo contarle sus ingresos ni sus traslados.
    /// </para>
    /// <para>
    /// ⚠️ Solo es válido ANTES del primer seguimiento (la apertura). <c>LoteAveEngordeId</c> NO
    /// distingue ciclos: el movimiento se etiqueta con el lote VIGENTE del galpón, así que el
    /// preiniciador del ciclo nuevo queda con el id del viejo mientras el nuevo no tenga
    /// seguimiento cargado (CAROLINA G0059: 2.800 kg del 16/06 atribuidos al lote anterior siendo
    /// del actual). Dentro del rango propio del lote, todo lo que entra al galpón es suyo.
    /// </para>
    /// <para>Un movimiento sin atribución (<c>LoteAveEngordeId is null</c>) NUNCA es ajeno: se
    /// conserva para no perder alimento real.</para>
    /// </summary>
    public static bool EsDeCicloAjeno(LoteRegistroHistoricoUnificado h, IReadOnlySet<int>? lotesAjenos)
        => lotesAjenos is { Count: > 0 }
           && h.LoteAveEngordeId.HasValue
           && lotesAjenos.Contains(h.LoteAveEngordeId.Value);

    /// <summary>
    /// Clasifica los demás lotes de un galpón en AJENOS (ciclo que no se solapa) vs convivientes.
    /// Espejo exacto del CTE <c>lotes_ajenos</c> de <c>fn_seguimiento_diario_engorde</c> (v11) y
    /// complemento del predicado «CONVIVEN» de <c>consumo_galpon_por_fecha</c> (v10):
    /// convive ⇔ <c>SegMin &lt;= hasta AND SegMax &gt;= desde</c>.
    /// <para>Un lote SIN seguimiento es ajeno por construcción: todavía no tiene ciclo.</para>
    /// </summary>
    /// <param name="ciclosDelGalpon">Los otros lotes del mismo (granja, núcleo, galpón), con su primer y último día de seguimiento.</param>
    /// <param name="desde">Primer día de seguimiento del lote consultado.</param>
    /// <param name="hasta">Último día de seguimiento del lote consultado.</param>
    public static HashSet<int> ResolverLotesAjenos(
        IEnumerable<(int LoteId, DateTime? SegMin, DateTime? SegMax)> ciclosDelGalpon,
        DateTime desde,
        DateTime hasta)
    {
        var ajenos = new HashSet<int>();
        foreach (var c in ciclosDelGalpon)
        {
            var convive = c.SegMin.HasValue && c.SegMax.HasValue
                       && c.SegMin.Value.Date <= hasta.Date
                       && c.SegMax.Value.Date >= desde.Date;
            if (!convive)
                ajenos.Add(c.LoteId);
        }
        return ajenos;
    }

    /// <summary>
    /// Último día de seguimiento del ciclo que ocupaba el galpón ANTES que este (v12).
    /// <c>null</c> si es el primer ciclo del galpón.
    /// <para>
    /// Complementa a <see cref="ResolverLotesAjenos"/>. Hacen falta los dos porque la atribución del
    /// histórico falla en ambos sentidos: <c>lote_ave_engorde_id</c> lo pone el trigger con
    /// <c>fn_lote_ave_engorde_id_desde_ubicacion</c>, que devuelve el lote de id MÁS ALTO del galpón
    /// al momento de insertar. La limpieza del ciclo anterior queda con el id del lote viejo si se
    /// registró antes de crear el nuevo (la caza <c>ResolverLotesAjenos</c>) o con el id del NUEVO si
    /// se registró después (la caza este corte). Caso SAN GUILLERMO G0033: dos traslados de salida del
    /// 13/03 por 5.160 kg, el mismo día en que cerró el ciclo previo, etiquetados con el lote nuevo.
    /// </para>
    /// </summary>
    /// <param name="ciclosDelGalpon">Los otros lotes del mismo (granja, núcleo, galpón).</param>
    /// <param name="desde">Primer día de seguimiento del lote consultado.</param>
    public static DateTime? ResolverFinCicloAnterior(
        IEnumerable<(int LoteId, DateTime? SegMin, DateTime? SegMax)> ciclosDelGalpon,
        DateTime desde)
    {
        DateTime? fin = null;
        foreach (var c in ciclosDelGalpon)
        {
            if (!c.SegMax.HasValue) continue;
            var max = c.SegMax.Value.Date;
            if (max >= desde.Date) continue;              // no cerró antes de que yo empezara
            if (fin is null || max > fin.Value) fin = max;
        }
        return fin;
    }

    /// <summary>
    /// Día en que arranca de verdad la ventana de apertura: el corte de la ventana previa al encaset
    /// (v9) o el día siguiente al fin del ciclo anterior, <b>el que sea más tarde</b>. Espejo del CTE
    /// <c>corte_apertura</c> de <c>fn_seguimiento_diario_engorde</c> (v12).
    /// </summary>
    public static DateTime? ResolverCorteApertura(DateTime? corteVentana, DateTime? finCicloAnterior)
    {
        if (finCicloAnterior is null) return corteVentana;
        var trasElCicloAnterior = finCicloAnterior.Value.Date.AddDays(1);
        if (corteVentana is null) return trasElCicloAnterior;
        return corteVentana.Value.Date >= trasElCicloAnterior ? corteVentana.Value.Date : trasElCicloAnterior;
    }
}
