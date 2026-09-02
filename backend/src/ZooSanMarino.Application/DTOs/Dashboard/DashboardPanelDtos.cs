namespace ZooSanMarino.Application.DTOs.Dashboard;

/// <summary>
/// Un día de una serie temporal.
///
/// <para>Sólo se emiten los días que <b>tienen registro</b>. Los días faltantes NO se rellenan con
/// cero: el front arma el eje completo del período y los deja como hueco. Un día sin seguimiento
/// cargado y un día con mortalidad cero son hechos distintos, y confundirlos inventa un dato.</para>
/// </summary>
/// <param name="Fecha">Fecha pura <c>YYYY-MM-DD</c>.</param>
/// <param name="Valor">Suma de lo CAPTURADO ese día en los lotes del alcance.</param>
public sealed record PuntoDiaDto(string Fecha, decimal Valor);

/// <summary>Una categoría de una distribución (una granja, un concepto, un estado).</summary>
public sealed record CategoriaDto(string Etiqueta, decimal Valor);

/// <summary>
/// Ventana de fechas de una consulta del dashboard, ya saneada.
/// </summary>
/// <param name="Desde">Inclusive.</param>
/// <param name="Hasta">Inclusive.</param>
public sealed record PeriodoDashboard(DateOnly Desde, DateOnly Hasta)
{
    /// <summary>Días de la ventana, extremos incluidos.</summary>
    public int Dias => Hasta.DayNumber - Desde.DayNumber + 1;
}

/// <summary>
/// Panel de POSTURA (levante + producción).
///
/// <para>Las tres series salen de columnas <b>capturadas</b> del seguimiento diario
/// (<c>mortalidad_hembras</c>, <c>mortalidad_machos</c>, <c>huevo_tot</c>), sumadas por día en la
/// base. No se recalcula ningún indicador derivado: los saldos, el % de producción y la comparación
/// contra la guía genética tienen dueño —las <c>fn_*</c> de producción— y una segunda cuenta acá
/// daría dos números distintos para lo mismo.</para>
/// </summary>
/// <param name="MortalidadDiaria">Aves muertas por día (hembras + machos).</param>
/// <param name="HuevoDiario">Huevo total por día.</param>
/// <param name="LotesPorGranja">Lotes de postura activos por granja.</param>
/// <param name="TotalMortalidad">Suma del período.</param>
/// <param name="TotalHuevo">Suma del período.</param>
/// <param name="DiasConRegistro">Días del período con al menos un seguimiento cargado.</param>
/// <param name="OcultaMachos">La empresa no maneja machos en postura: la serie viene sólo de hembras.</param>
public sealed record DashboardPosturaDto(
    IReadOnlyList<PuntoDiaDto> MortalidadDiaria,
    IReadOnlyList<PuntoDiaDto> HuevoDiario,
    IReadOnlyList<CategoriaDto> LotesPorGranja,
    decimal TotalMortalidad,
    decimal TotalHuevo,
    int DiasConRegistro,
    bool OcultaMachos
)
{
    public static DashboardPosturaDto Vacio(bool ocultaMachos) => new(
        Array.Empty<PuntoDiaDto>(), Array.Empty<PuntoDiaDto>(), Array.Empty<CategoriaDto>(),
        0, 0, 0, ocultaMachos);
}

/// <summary>
/// Panel de POLLO ENGORDE.
///
/// <para>Mismo criterio que postura: se suman columnas capturadas del seguimiento diario. El peso
/// promedio es el <b>promedio de los pesos cargados ese día</b> —no un peso ponderado por aves—, y
/// se dice así en la pantalla: ponderarlo exigiría el saldo de aves, que es de
/// <c>fn_seguimiento_diario_engorde</c>.</para>
/// </summary>
/// <param name="MortalidadDiaria">Aves muertas por día.</param>
/// <param name="ConsumoDiarioKg">Kilos de alimento consumidos por día.</param>
/// <param name="PesoPromedioDiario">Promedio simple de los pesos cargados ese día, en gramos.</param>
/// <param name="LotesPorGranja">Lotes de engorde activos por granja.</param>
/// <param name="TotalMortalidad">Suma del período.</param>
/// <param name="TotalConsumoKg">Suma del período.</param>
/// <param name="DiasConRegistro">Días del período con al menos un seguimiento cargado.</param>
public sealed record DashboardEngordeDto(
    IReadOnlyList<PuntoDiaDto> MortalidadDiaria,
    IReadOnlyList<PuntoDiaDto> ConsumoDiarioKg,
    IReadOnlyList<PuntoDiaDto> PesoPromedioDiario,
    IReadOnlyList<CategoriaDto> LotesPorGranja,
    decimal TotalMortalidad,
    decimal TotalConsumoKg,
    int DiasConRegistro
)
{
    public static DashboardEngordeDto Vacio() => new(
        Array.Empty<PuntoDiaDto>(), Array.Empty<PuntoDiaDto>(), Array.Empty<PuntoDiaDto>(),
        Array.Empty<CategoriaDto>(), 0, 0, 0);
}

/// <summary>
/// Un galpón cuyo saldo de alimento no cuadra, tal como lo reporta
/// <c>fn_cuadre_alimento_engorde</c>.
///
/// <para>🔴 <b>Las dos señales van SEPARADAS y no se suman.</b> <see cref="DescuadreKg"/> son KILOS
/// que faltan o sobran; <see cref="FilasNegativas"/> son DÍAS que cerraron en rojo con el total
/// perfecto (está mal el orden o la fecha de los ingresos). Mezclarlas en un solo número es el error
/// que CLAUDE.md documenta: la consulta que las unía daba 23 galpones cuando los que tenían kilos
/// eran 8 — los otros 15 entraban con un descuadre de ~1e-11, o sea cero.</para>
/// </summary>
public sealed record DescuadreGalponDto(
    string GranjaNombre,
    string GalponId,
    decimal DescuadreKg,
    int FilasNegativas,
    int CiclosDelGalpon
);

/// <summary>Panel de ALIMENTO E INVENTARIO.</summary>
/// <param name="StockPorGranja">Kilos/unidades en existencia por granja.</param>
/// <param name="Descuadres">Galpones con descuadre de alimento. Vacío = el invariante se cumple.</param>
/// <param name="GalponesConKilos">Cuántos descuadres son de KILOS (no de orden de fechas).</param>
/// <param name="GalponesConDiasEnRojo">Cuántos son sólo días en rojo, con el total correcto.</param>
public sealed record DashboardInventarioDto(
    IReadOnlyList<CategoriaDto> StockPorGranja,
    IReadOnlyList<DescuadreGalponDto> Descuadres,
    int GalponesConKilos,
    int GalponesConDiasEnRojo
)
{
    public static DashboardInventarioDto Vacio() => new(
        Array.Empty<CategoriaDto>(), Array.Empty<DescuadreGalponDto>(), 0, 0);
}

/// <summary>Panel de CUMPLIMIENTO Y PENDIENTES.</summary>
/// <param name="VacunacionVencida">Ítems del cronograma cuya fecha ya pasó y no se aplicaron.</param>
/// <param name="VacunacionProxima">Ítems que vencen dentro del horizonte (7 días).</param>
/// <param name="CuadresSinResolver">Filas <c>requiere_cuadre</c> del push offline sin marcar.</param>
/// <param name="VacunacionPorGranja">Pendientes de vacunación agrupados por granja.</param>
public sealed record DashboardCumplimientoDto(
    int VacunacionVencida,
    int VacunacionProxima,
    int CuadresSinResolver,
    IReadOnlyList<CategoriaDto> VacunacionPorGranja
)
{
    public static DashboardCumplimientoDto Vacio() => new(0, 0, 0, Array.Empty<CategoriaDto>());
}
