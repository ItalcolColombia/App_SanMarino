namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Aritmética PURA del descuento de aves que produce un seguimiento diario sobre el saldo del lote
/// (levante y producción).
///
/// <para>
/// Se extrajo al mover la regla de levante desde <c>SeguimientoLoteLevanteService</c> hacia
/// <c>SeguimientoDiarioService</c> (item A7). El motivo de que exista como cálculo aparte no es
/// estético: los dos módulos hacían la misma cuenta escrita de dos formas distintas —uno aplicaba el
/// <b>delta neto</b> (<c>viejo − nuevo</c>) y el otro <b>revierte y vuelve a aplicar</b>— y antes de
/// unificarlos había que demostrar que dan el mismo resultado, clamp incluido. Esa demostración es
/// <c>RevertirYAplicarEsIgualAlDeltaNeto</c> en los tests.
/// </para>
/// </summary>
public static class DescuentoAvesSeguimientoCalculos
{
    /// <summary>
    /// Aves que un seguimiento saca del saldo: <b>mortalidad + selección + error de sexaje</b>.
    ///
    /// <para>
    /// Los tres componentes van juntos siempre. Es la definición que ya usaban por separado el
    /// módulo de levante y el service de seguimiento; tenerla en un solo lugar evita que una de las
    /// dos copias se olvide del error de sexaje en el próximo cambio — que es justo el tipo de
    /// divergencia que motivó A7.
    /// </para>
    /// </summary>
    public static int TotalDescuento(int mortalidad, int seleccion, int errorSexaje) =>
        mortalidad + seleccion + errorSexaje;

    /// <summary>
    /// Aplica un delta al saldo, sin dejarlo negativo.
    ///
    /// <para>
    /// El <c>Math.Max(0, …)</c> es el comportamiento histórico y se conserva **tal cual**. Vale
    /// dejar dicho que ese clamp es lo que hace la operación <b>no reversible aritméticamente</b>: si
    /// el saldo llega a 0 y se sigue descontando, revertir después no devuelve al valor original. Es
    /// una de las razones por las que la escritura offline (F3) está bloqueada — ver
    /// <c>fase_de_desarrollo/pwa_offline_first_plan.md</c> §2.2. Cambiarlo acá sin más movería
    /// saldos históricos, así que no se toca en este item.
    /// </para>
    /// </summary>
    public static int AplicarDelta(int saldoActual, int delta) => System.Math.Max(0, saldoActual + delta);

    /// <summary>
    /// Saldo resultante de EDITAR un seguimiento: se revierte lo que descontaba el valor viejo y se
    /// aplica lo que descuenta el nuevo. Es la forma que usa <c>SeguimientoDiarioService.UpdateAsync</c>.
    /// </summary>
    public static int SaldoTrasEdicion(int saldoActual, int descuentoViejo, int descuentoNuevo)
    {
        var revertido = AplicarDelta(saldoActual, descuentoViejo);   // sumar lo que se había restado
        return AplicarDelta(revertido, -descuentoNuevo);             // restar lo nuevo
    }

    /// <summary>
    /// Saldo resultante de aplicar directamente el <b>delta neto</b> (<c>viejo − nuevo</c>), que es
    /// la forma que usaba el módulo de levante antes de A7. Se conserva para poder probar en un test
    /// que las dos formas coinciden.
    /// </summary>
    public static int SaldoTrasEdicionPorDeltaNeto(int saldoActual, int descuentoViejo, int descuentoNuevo) =>
        AplicarDelta(saldoActual, descuentoViejo - descuentoNuevo);
}
