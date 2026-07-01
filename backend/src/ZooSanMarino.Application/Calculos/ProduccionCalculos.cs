namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculos puros (sin EF ni estado) de los indicadores de producción de reproductoras (postura),
/// alineados a las fórmulas del requerimiento REQ-004 y comparables campo a campo con la guía
/// genética (guia_genetica_sanmarino_colombia): % producción, H.T.A.A, H.I.A.A, gr/ave/día,
/// consumo acumulado y % de retiro. Extraídos para ser deterministas y testeables.
/// </summary>
public static class ProduccionCalculos
{
    /// <summary>
    /// % Producción (hen-day) = huevos del día / saldo de HEMBRAS vivas × 100.
    /// Los machos no ponen huevos ⇒ se excluyen del denominador para que cuadre con
    /// prod_porcentaje de la guía (que es por hembra).
    /// </summary>
    public static decimal PorcentajeProduccion(decimal huevosPorDia, int hembrasVivas)
        => hembrasVivas > 0 ? huevosPorDia / hembrasVivas * 100m : 0m;

    /// <summary>
    /// H.T.A.A (Huevos Totales por Ave Alojada) = Σ huevos totales acumulados / hembras iniciales.
    /// Es un acumulado por hembra alojada; se compara contra h_total_aa de la guía.
    /// </summary>
    public static decimal Htaa(long huevosTotalesAcumulados, int hembrasIniciales)
        => hembrasIniciales > 0 ? (decimal)huevosTotalesAcumulados / hembrasIniciales : 0m;

    /// <summary>
    /// H.I.A.A (Huevos Incubables por Ave Alojada) = Σ incubables acumulados / hembras iniciales.
    /// Se compara contra h_inc_aa de la guía.
    /// </summary>
    public static decimal Hiaa(long huevosIncubablesAcumulados, int hembrasIniciales)
        => hembrasIniciales > 0 ? (decimal)huevosIncubablesAcumulados / hembrasIniciales : 0m;

    /// <summary>
    /// Gramos / Ave / Día = (alimento de la semana en kg × 1000 / días) / saldo de aves.
    /// Es el consumo diario promedio (NO la sumatoria semanal); se compara con gr_ave_dia_h/m.
    /// </summary>
    public static decimal GramosAveDia(decimal consumoSemanaKg, int saldoAves, int diasSemana = 7)
        => (saldoAves > 0 && diasSemana > 0)
            ? (consumoSemanaKg * 1000m / diasSemana) / saldoAves
            : 0m;

    /// <summary>
    /// Consumo acumulado g/ave = alimento acumulado en kg × 1000 / aves iniciales.
    /// Se compara con cons_ac_h/m de la guía.
    /// </summary>
    public static decimal ConsumoAcumuladoGrAve(decimal consumoAcumuladoKg, int avesIniciales)
        => avesIniciales > 0 ? consumoAcumuladoKg * 1000m / avesIniciales : 0m;

    /// <summary>
    /// % Retiro semanal = (mortalidad + selección de la semana) / saldo de aves de la semana × 100.
    /// Reemplaza al "% pérdidas del día". Se compara con mort_sem_h/m + selección de la guía.
    /// </summary>
    public static decimal PorcentajeRetiroSemanal(int mortalidadSemana, int seleccionSemana, int saldoSemana)
        => saldoSemana > 0 ? (decimal)(mortalidadSemana + seleccionSemana) / saldoSemana * 100m : 0m;

    /// <summary>
    /// % Retiro acumulado = (mortalidad acum. + selección acum.) / aves iniciales × 100.
    /// Se compara con retiro_ac_h/m de la guía.
    /// </summary>
    public static decimal PorcentajeRetiroAcumulado(int mortalidadAcumulada, int seleccionAcumulada, int avesIniciales)
        => avesIniciales > 0 ? (decimal)(mortalidadAcumulada + seleccionAcumulada) / avesIniciales * 100m : 0m;
}
