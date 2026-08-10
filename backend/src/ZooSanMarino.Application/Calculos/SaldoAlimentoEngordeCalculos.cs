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
    /// al momento de insertar. (Desde la migración <c>FnLoteEngordeDesdeUbicacionExcluyeLiquidados</c>
    /// esa función ya no puede devolver un lote <b>liquidado</b> —item A9—, pero sigue sin mirar la
    /// fecha de la operación: entre dos lotes VIVOS del mismo galpón elige el de id más alto, así que
    /// estos dos cortes siguen siendo necesarios. Y no arregla nada de lo ya grabado.) La limpieza del ciclo anterior queda con el id del lote viejo si se
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

    /// <summary>
    /// Primer día de seguimiento del ciclo que ocupa el galpón DESPUÉS que este (v14).
    /// <c>null</c> si es el último ciclo del galpón o si el lote consultado todavía no tiene
    /// seguimiento.
    /// <para>
    /// Espejo del CTE <c>corte_ciclo_siguiente</c> y complemento exacto de
    /// <see cref="ResolverFinCicloAnterior"/>: si nada anterior al fin del ciclo previo es alimento
    /// mío, nada posterior al arranque del ciclo que me sucede lo es tampoco.
    /// </para>
    /// <para>
    /// La comparación es ESTRICTA a propósito: un lote que CONVIVE conmigo en el galpón (v10) empieza
    /// antes de que yo termine, así que nunca corta nada y el saldo compartido queda intacto.
    /// </para>
    /// </summary>
    /// <param name="ciclosDelGalpon">Los otros lotes del mismo (granja, núcleo, galpón).</param>
    /// <param name="hasta">Último día de seguimiento del lote consultado.</param>
    public static DateTime? ResolverInicioCicloSiguiente(
        IEnumerable<(int LoteId, DateTime? SegMin, DateTime? SegMax)> ciclosDelGalpon,
        DateTime? hasta)
    {
        if (hasta is null) return null;
        DateTime? inicio = null;
        foreach (var c in ciclosDelGalpon)
        {
            if (!c.SegMin.HasValue) continue;
            var min = c.SegMin.Value.Date;
            if (min <= hasta.Value.Date) continue;           // convive conmigo o arrancó antes
            if (inicio is null || min < inicio.Value) inicio = min;
        }
        return inicio;
    }

    /// <summary>
    /// Último día que muestra la grilla diaria del lote. Espejo del CTE <c>rango_final</c>.
    /// <list type="bullet">
    ///   <item>cierre efectivo por saldo de alimento en 0 (v5), si lo hay;</item>
    ///   <item>si no, el último seguimiento cuando el lote está marcado como cerrado (v5);</item>
    ///   <item>y nunca más allá del día previo al arranque del ciclo siguiente del galpón (v14).</item>
    /// </list>
    /// <c>null</c> = sin tope (lote genuinamente activo). Espeja el <c>LEAST</c> de SQL, que ignora
    /// los NULL: un lote sin ciclo posterior conserva exactamente el corte previo a v14.
    /// </summary>
    public static DateTime? ResolverFechaMaxGrilla(
        DateTime? cierrePorSaldoCero,
        bool loteCerrado,
        DateTime? ultimoSeguimiento,
        DateTime? inicioCicloSiguiente)
    {
        var previo = cierrePorSaldoCero?.Date
                  ?? (loteCerrado ? ultimoSeguimiento?.Date : null);
        var porCicloSiguiente = inicioCicloSiguiente?.Date.AddDays(-1);

        if (previo is null) return porCicloSiguiente;
        if (porCicloSiguiente is null) return previo;
        return previo.Value <= porCicloSiguiente.Value ? previo : porCicloSiguiente;
    }

    /// <summary>
    /// ¿El movimiento entra a la APERTURA de este ciclo por la marca explícita
    /// «este alimento es para el próximo encasetamiento del galpón»? (v15, 2026-08-08)
    /// <para>
    /// Espejo del primer disyunto del filtro de <c>apert_mov</c> en
    /// <c>fn_seguimiento_diario_engorde</c>. La marca la pone la persona que registra el ingreso, así
    /// que SUSTITUYE a los dos cortes por fecha (<see cref="ResolverCorteApertura"/> de v12 y
    /// <see cref="EsDeCicloAjeno"/> de v11): es la única atribución posible en los galpones
    /// ENCADENADOS de Ecuador, donde la llegada real cae DENTRO del ciclo anterior y la fecha sola no
    /// alcanza para decidir de quién es el alimento.
    /// </para>
    /// <para>
    /// Guarda: lo absorbe el PRIMER ciclo del galpón que arranca después del movimiento. Si otro lote
    /// de la misma ubicación ya empezó su seguimiento ENTRE la fecha del movimiento y
    /// <paramref name="miPrimerSeguimiento"/>, la marca era para aquél — una marca vieja no se cuela
    /// dos ciclos después. Un lote sin seguimiento (<c>SegMin</c> nulo) nunca bloquea.
    /// </para>
    /// </summary>
    /// <param name="marcado">Valor de <c>para_proximo_ciclo</c> del movimiento.</param>
    /// <param name="fechaMovimiento">Fecha de operación del movimiento.</param>
    /// <param name="miPrimerSeguimiento">Primer día de seguimiento del lote consultado.</param>
    /// <param name="ciclosDelGalpon">Los otros lotes del mismo (granja, núcleo, galpón).</param>
    public static bool EntraPorMarcaProximoCiclo(
        bool marcado,
        DateTime fechaMovimiento,
        DateTime miPrimerSeguimiento,
        IEnumerable<(int LoteId, DateTime? SegMin, DateTime? SegMax)> ciclosDelGalpon)
    {
        if (!marcado) return false;

        var mov = fechaMovimiento.Date;
        var mio = miPrimerSeguimiento.Date;
        if (mov >= mio) return false;                       // la apertura solo mira lo anterior al día 1

        foreach (var c in ciclosDelGalpon)
        {
            if (!c.SegMin.HasValue) continue;
            var min = c.SegMin.Value.Date;
            if (min > mov && min < mio) return false;        // otro ciclo se interpuso: la marca era suyo
        }
        return true;
    }

    /// <summary>
    /// ¿El movimiento debe SALIR de la fila diaria del ciclo que lo contiene? (v15, 2026-08-08)
    /// <para>
    /// Espejo del filtro <c>rs.fecha_min IS NULL OR NOT para_proximo_ciclo</c> que la fn aplica en
    /// <c>hist_alimento</c>, <c>hist_full</c>, <c>docs_por_fecha</c> y <c>fechas_universo</c>: un
    /// movimiento marcado no es kg de este ciclo aunque su fecha caiga en el rango — su lugar es la
    /// apertura del siguiente.
    /// </para>
    /// <para>
    /// EXCEPCIÓN deliberada: mientras el lote no tiene seguimiento
    /// (<paramref name="miPrimerSeguimiento"/> nulo) el movimiento se conserva visible. Todavía no
    /// hay apertura donde absorberlo y los kg no pueden desaparecer de la pantalla — ese parpadeo es
    /// justamente lo que empuja a la operación a re-fechar el ingreso al día 1.
    /// </para>
    /// </summary>
    public static bool ExcluidoDeFilaDiariaPorMarca(bool marcado, DateTime? miPrimerSeguimiento)
        => marcado && miPrimerSeguimiento.HasValue;
}
