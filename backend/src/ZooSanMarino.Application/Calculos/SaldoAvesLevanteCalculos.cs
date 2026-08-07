// src/ZooSanMarino.Application/Calculos/SaldoAvesLevanteCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Saldo de aves vivas de un lote de LEVANTE a partir de sus registros diarios.
///
/// <para>
/// Esta clase es la <b>especificación ejecutable</b> de una fórmula que ya vive en la base de
/// datos, y existe para que no haya dos versiones del mismo número (regla «una sola fórmula por
/// número» de <c>CLAUDE.md</c>). Los dos dueños del saldo en BD son:
/// </para>
/// <list type="bullet">
///   <item><c>fn_reporte_semanal_levante_extras</c>:
///     <c>fin_h := acum_h − mort_h − sel_h − err_h − traslado_salida_h + traslado_ingreso_h</c></item>
///   <item><c>fn_migracion_seguimiento_levante</c>, que escribe
///     <c>lote_postura_levante.aves_h_actual = GREATEST(0, inicial − Σ(mort + sel + error_sexaje))</c></item>
/// </list>
///
/// <para>
/// El <b>error de sexaje</b> es una baja igual que la mortalidad y la selección: son aves que
/// dejan de contarse en ese sexo (se descubrió que estaban mal sexadas). Omitirlo infla el saldo
/// y, en cascada, subestima el consumo por ave — que es lo que pasaba en el reporte técnico de
/// levante antes de este cálculo (cerraba 614 aves por encima del maestro en un lote de 20.458).
/// </para>
/// </summary>
public static class SaldoAvesLevanteCalculos
{
    /// <summary>Bajas y movimientos de aves de un día (o de una semana ya agregada), por sexo.</summary>
    /// <param name="Mortalidad">Aves muertas.</param>
    /// <param name="Seleccion">Aves descartadas por selección.</param>
    /// <param name="ErrorSexaje">Aves que salen del sexo por corrección de sexaje.</param>
    /// <param name="TrasladoSalida">Aves trasladadas a otro lote.</param>
    /// <param name="TrasladoIngreso">Aves recibidas de otro lote.</param>
    /// <param name="Venta">Aves vendidas (descarte). Salen del lote igual que un traslado, pero no
    /// llegan a ningún otro lote. En producción venían quedando fuera del saldo porque la venta solo
    /// dejaba una nota de texto en la fila diaria.</param>
    /// <param name="Retiro">Aves retiradas por otros motivos registrados en <c>movimiento_aves</c>.</param>
    public readonly record struct MovimientoDia(
        int Mortalidad,
        int Seleccion,
        int ErrorSexaje,
        int TrasladoSalida = 0,
        int TrasladoIngreso = 0,
        int Venta = 0,
        int Retiro = 0);

    /// <summary>
    /// Bajas netas del período:
    /// <c>mortalidad + selección + error de sexaje + salidas + ventas + retiros − ingresos</c>.
    /// Puede ser negativa si el lote recibió más aves de las que perdió.
    /// </summary>
    public static int BajasNetas(MovimientoDia m) =>
        m.Mortalidad + m.Seleccion + m.ErrorSexaje + m.TrasladoSalida + m.Venta + m.Retiro - m.TrasladoIngreso;

    /// <summary>
    /// Saldo tras aplicar el movimiento, con piso en 0 (mismo <c>GREATEST(0, …)</c> que la fn SQL:
    /// un histórico mal cuadrado no debe producir aves negativas).
    /// </summary>
    public static int Siguiente(int saldoPrevio, MovimientoDia m) =>
        Math.Max(0, saldoPrevio - BajasNetas(m));

    /// <summary>
    /// Saldo final partiendo del encasetamiento y aplicando la secuencia completa. Equivale a
    /// <c>Siguiente</c> aplicado en orden, pero sin el piso intermedio: el piso se aplica una sola
    /// vez al final, igual que el <c>UPDATE</c> agregado de <c>fn_migracion_seguimiento_levante</c>.
    /// </summary>
    public static int SaldoFinal(int avesIniciales, IEnumerable<MovimientoDia> movimientos)
    {
        var bajas = 0;
        foreach (var m in movimientos) bajas += BajasNetas(m);
        return Math.Max(0, avesIniciales - bajas);
    }

    /// <summary>
    /// Retiro acumulado (mortalidad + selección + error de sexaje) sobre el que se calcula el
    /// «% retiro acumulado» de los reportes. NO incluye traslados: un traslado mueve aves entre
    /// lotes, no las retira del ciclo. Tampoco ventas: se reportan aparte.
    /// </summary>
    public static int RetiroAcumulado(IEnumerable<MovimientoDia> movimientos)
    {
        var total = 0;
        foreach (var m in movimientos) total += m.Mortalidad + m.Seleccion + m.ErrorSexaje;
        return total;
    }
}
