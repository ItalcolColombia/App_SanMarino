namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo PURO del saldo físico de aves de LEVANTE tras la convergencia a Feature-13.
///
/// Fuente de verdad de la aritmética (todos los lectores se alinean a ella):
///   LoteService.GetMortalidadResumenAsync  →  saldo = base − mortCaja − mort − sel − err + trasIn − trasOut
///   fn_indicadores_levante_postura / sp_recalcular_seguimiento_levante (mismo out por semana)
///   IndicadorEcuadorService.CalcularAvesActualesAsync (ventas restan vía "sacrificadas")
///
/// Convergencia: el traslado sale por columnas dedicadas (traslado_salida/ingreso), NO por ±Sel;
/// la selección pasa a ser SIEMPRE genuina (cull real). Esto CORRIGE el signo del saldo para lotes
/// con traslados (el hack ±Sel lo sumaba con signo invertido).
/// </summary>
public static class SaldoLevanteCalculos
{
    /// <summary>
    /// Saldo físico de aves (una sola fase). Nunca negativo.
    /// base − mortCaja − mortalidad − selección − errorSexaje + trasladoIngreso − trasladoSalida.
    /// </summary>
    public static int SaldoFisico(int baseAves, int mortCaja, int mortalidad, int seleccion,
                                  int errorSexaje, int trasladoIngreso, int trasladoSalida)
        => System.Math.Max(0, baseAves - mortCaja - mortalidad - seleccion - errorSexaje
                              + trasladoIngreso - trasladoSalida);

    /// <summary>Salidas de la semana (lo que resta del saldo): mort + sel + err + tras_salida − tras_ingreso.</summary>
    public static int SalidasSemana(int mortalidad, int seleccion, int errorSexaje,
                                    int trasladoSalida, int trasladoIngreso)
        => mortalidad + seleccion + errorSexaje + trasladoSalida - trasladoIngreso;

    /// <summary>Traslado NETO (positivo = más salidas que ingresos ⇒ resta del saldo).</summary>
    public static int TrasladoNeto(int trasladoSalida, int trasladoIngreso)
        => trasladoSalida - trasladoIngreso;

    /// <summary>
    /// Aves actuales según IndicadorEcuadorService: iniciales − mortalidad − selección genuina
    /// − sacrificadas (ventas/despachos vía movimiento_aves, restan UNA vez) − traslado neto.
    /// Nunca negativo.
    /// </summary>
    public static int AvesActualesEcuador(int iniciales, int mortalidad, int seleccion,
                                          int sacrificadas, int trasladoNeto)
        => System.Math.Max(0, iniciales - mortalidad - seleccion - sacrificadas - trasladoNeto);

    /// <summary>% de selección GENUINA sobre la base (el traslado ya NO infla la selección).</summary>
    public static decimal PorcSeleccion(int seleccionGenuina, int baseAves)
        => baseAves > 0 ? (decimal)seleccionGenuina / baseAves * 100m : 0m;
}
