// Espejo en C# de la función SQL `fn_tipo_evento_inventario`, que traduce el
// `movement_type` de inventario_gestion_movimiento al `tipo_evento` con el que la fila queda en
// lote_registro_historico_unificado (lo escribe el trigger trg_inventario_gestion_movimiento_lote_hist).
//
// Vive acá, puro y con tests, porque de ese mapeo depende una decisión que si no queda escrita se
// pudre en silencio: QUÉ movimientos de inventario obligan a refrescar el saldo de alimento de los
// lotes de pollo engorde del galpón. Si mañana alguien agrega un movement_type nuevo y no lo mapea,
// el test lo delata en vez de que aparezca meses después como un descuadre en pantalla.
namespace ZooSanMarino.Application.Calculos;

public static class TipoEventoInventarioCalculos
{
    public const string InvIngreso         = "INV_INGRESO";
    public const string InvTrasladoEntrada = "INV_TRASLADO_ENTRADA";
    public const string InvTrasladoSalida  = "INV_TRASLADO_SALIDA";
    public const string InvConsumo         = "INV_CONSUMO";
    public const string InvOtro            = "INV_OTRO";

    /// <summary>
    /// Traduce el <c>movement_type</c> al <c>tipo_evento</c> del histórico unificado.
    /// Comparación sin distinguir mayúsculas, igual que el <c>ILIKE</c> de la función SQL.
    /// Cualquier tipo desconocido cae en <see cref="InvOtro"/>.
    /// </summary>
    public static string TipoEvento(string? movementType)
    {
        if (string.IsNullOrWhiteSpace(movementType)) return InvOtro;
        var mt = movementType.Trim();

        if (Igual(mt, "Ingreso")) return InvIngreso;

        if (Igual(mt, "TrasladoEntrada") || Igual(mt, "TrasladoInterGranjaEntrada"))
            return InvTrasladoEntrada;

        if (Igual(mt, "TrasladoSalida") || Igual(mt, "TrasladoInterGranjaSalida")
            || Igual(mt, "TrasladoInterGranjaPendiente"))
            return InvTrasladoSalida;

        if (Igual(mt, "Consumo")) return InvConsumo;

        return InvOtro;

        static bool Igual(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ¿Este movimiento cambia el saldo de alimento de los lotes de pollo engorde del galpón?
    /// <para>
    /// Solo las ENTRADAS y SALIDAS de alimento cuentan. Quedan fuera a propósito:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Consumo</b> (<c>INV_CONSUMO</c>): el saldo resta el consumo de
    /// <c>seguimiento_diario_aves_engorde</c>, no el del inventario. Contarlo acá lo descontaría dos veces.</item>
    /// <item><b>AjusteStock y EliminacionStock</b> (<c>INV_OTRO</c>): ningún cálculo del saldo mira ese
    /// tipo de evento, y además el ajuste guarda la cantidad en valor absoluto, sin el signo del delta.</item>
    /// </list>
    /// </summary>
    public static bool AfectaSaldoAlimentoEngorde(string? movementType)
        => TipoEvento(movementType) is InvIngreso or InvTrasladoEntrada or InvTrasladoSalida;
}
