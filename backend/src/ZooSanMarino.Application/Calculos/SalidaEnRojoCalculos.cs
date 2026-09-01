namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Avisa cuando un traslado de SALIDA dejaría algún día de la tabla diaria del galpón en rojo.
///
/// <para>
/// 🔴 <b>Por qué existe.</b> El stock se valida <b>atómicamente</b> —es físico, y
/// <c>DescontarStockAtomicoAsync</c> rechaza si no alcanza—, pero la tabla diaria se arma
/// <b>por fecha declarada</b>. Esas dos cosas no son lo mismo: un ingreso registrado <i>hoy</i>
/// puede financiar una salida fechada tres días atrás, y el stock lo acepta porque en el instante
/// del guardado los kilos ya están. La tabla, que ordena por día, muestra la salida antes que el
/// ingreso y el día cierra <b>negativo</b>.
/// </para>
///
/// <para>
/// <b>Medido en producción</b> (18-may-2026, entre las 14:51 y las 15:08): en tres
/// galpones de Ecuador se cargó primero un <c>Ingreso</c> sin remisión fechado <i>ese mismo día</i> y
/// después las salidas fechadas hacia atrás, al 15 y 16 de mayo. Los tres lotes cerraron su último
/// día en rojo —G0055 en −3.920 kg, G0051 en −3.220, G0052 en −600— y nadie se enteró hasta que se
/// miró el dato tres meses después. Este aviso lo habría dicho en el momento, que es cuando la
/// persona todavía sabe qué pasó.
/// </para>
///
/// <para>
/// <b>Aviso confirmable, no bloqueo.</b> Registrar el movimiento igual puede ser lo correcto: quizá
/// el ingreso que lo respalda entra después, o la fecha se corrige a continuación. Bloquear obligaría
/// a inventar un orden que la operación no tiene por qué seguir, y el antecedente está a la vista —el
/// operador que no puede registrar lo que pasó termina cargando un ingreso sin remisión para que el
/// stock alcance—. El usuario ve el número, decide, y reenvía con la bandera.
/// </para>
///
/// <para>
/// <b>El saldo lo dice la fn, no este cálculo.</b> Acá solo vive la comparación; los kilos
/// disponibles salen de <c>fn_seguimiento_diario_engorde</c>, que es la dueña del número. Calcularlo
/// en C# sería una segunda fórmula para el mismo dato, que es exactamente cómo este módulo se rompió
/// antes.
/// </para>
/// </summary>
public static class SalidaEnRojoCalculos
{
    /// <summary>
    /// Tolerancia en kg, la misma de <see cref="CuadreAlimentoEngordeCalculos.ToleranciaKg"/>: por
    /// debajo de un kilo no es un día en rojo, es aritmética de punto flotante. Sin esto, un galpón
    /// que cierra en −1e-11 avisaría en cada salida.
    /// </summary>
    public const decimal ToleranciaKg = CuadreAlimentoEngordeCalculos.ToleranciaKg;

    /// <summary>
    /// ¿Vale la pena preguntarle a la fn por este traslado?
    ///
    /// <para>
    /// Solo si saca kilos <b>de un galpón</b>: la tabla diaria de engorde se arma por galpón, así que
    /// una salida de bodega de granja (sin galpón) no puede dejar ningún día en rojo —es el mismo
    /// criterio de <c>SaldoAlimentoEngordeAplicador.RecalcularPorUbicacionAsync</c>, que también se
    /// va sin hacer nada cuando no hay galpón—.
    /// </para>
    /// </summary>
    public static bool AmeritaChequeo(string? galponOrigen, decimal cantidad, bool confirmadoPorElUsuario)
    {
        if (confirmadoPorElUsuario) return false;
        if (cantidad <= 0) return false;
        return !string.IsNullOrWhiteSpace(galponOrigen);
    }

    /// <summary>
    /// ¿Sacar <paramref name="cantidad"/> kg deja algún día en rojo?
    ///
    /// <para>
    /// <paramref name="saldoMinimoDesdeLaFecha"/> es el <b>mínimo</b> de la tabla desde el día del
    /// movimiento en adelante, no el saldo de ese día: la salida baja por igual todos los días
    /// siguientes, así que un día posterior puede quedar en rojo aunque el del movimiento aguante.
    /// </para>
    ///
    /// <para>
    /// <c>null</c> ⇒ el galpón no tiene ningún día cargado desde esa fecha (no hay tabla que poner en
    /// rojo) y no se avisa nada.
    /// </para>
    /// </summary>
    public static bool DejaDiaEnRojo(decimal? saldoMinimoDesdeLaFecha, decimal cantidad)
    {
        if (saldoMinimoDesdeLaFecha is not { } saldo) return false;
        return saldo - cantidad < -ToleranciaKg;
    }

    /// <summary>
    /// Mensaje del aviso. Dice los tres números que la persona necesita para decidir —lo que hay, lo
    /// que sale y con cuánto queda— y en qué lote y día, para que pueda ir a mirarlo.
    /// </summary>
    public static string Mensaje(
        string? loteNombre, DateOnly fechaDelMinimo, decimal saldoMinimo, decimal cantidad, string? unidad)
    {
        var u = string.IsNullOrWhiteSpace(unidad) ? "kg" : unidad.Trim();
        var lote = string.IsNullOrWhiteSpace(loteNombre) ? "" : $" del lote {loteNombre.Trim()}";
        var queda = saldoMinimo - cantidad;

        return $"El {fechaDelMinimo:dd/MM/yyyy}{lote} la tabla diaria tiene {saldoMinimo:N1} {u} y esta " +
               $"salida retira {cantidad:N1} {u}: ese día quedaría en {queda:N1} {u}. " +
               "Suele significar que el ingreso que respalda estos kilos todavía no está cargado, o que " +
               "quedó con una fecha posterior a la de la salida. Revise la fecha antes de continuar.";
    }
}
