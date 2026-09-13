// src/ZooSanMarino.Application/Calculos/PorcentajeProduccionCalculos.cs
// %Producción (hen-day) del Reporte Técnico de producción — ver
// fase_de_desarrollo/reporte_tecnico_porcentaje_produccion_plan.md.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// %Producción ave-día: cuántos huevos pone, en promedio, cada hembra viva por día. Es la misma
/// definición que <c>fn_indicadores_produccion_postura</c> (REQ-004a) y la que traen las guías
/// genéticas (<c>prod_porcentaje</c>), por eso es comparable con la guía y no pasa de 100.
/// </summary>
public static class PorcentajeProduccionCalculos
{
    /// <summary>
    /// Hembras vivas al INICIO del día = cierre del día anterior (iniciales − mortalidad y selección
    /// acumuladas ANTES de ese día), sin bajar de 0. Es el denominador correcto: los huevos del día
    /// los pusieron las aves presentes, incluidas las que salieron ese día. Dividir por el cierre
    /// pasa de 100 % en un día de descarte masivo o liquidación.
    /// </summary>
    public static int HembrasInicioDia(int hembrasIniciales, int mortalidadAcumuladaPrevia, int seleccionAcumuladaPrevia)
        => Math.Max(0, hembrasIniciales - mortalidadAcumuladaPrevia - seleccionAcumuladaPrevia);

    /// <summary>Un día: huevos / hembras vivas al inicio del día × 100. Sin hembras ⇒ 0.</summary>
    public static double Diario(int huevos, int hembras)
        => hembras > 0 ? (double)huevos / hembras * 100d : 0d;

    /// <summary>
    /// Un período (semana, consolidado de galpones): Σ huevos / Σ aves-día × 100, donde las aves-día
    /// son las hembras vivas de cada día registrado. Los días sin hembras no entran ni en el
    /// numerador ni en el denominador. Período vacío ⇒ 0.
    /// </summary>
    /// <remarks>
    /// Dividir los huevos de la semana entre las aves de UN día (lo que hacía la Semanal General)
    /// infla el número ~7 veces: 19.213 huevos / 7.586 hembras = 253,3 % en lugar de 36,2 %.
    /// </remarks>
    public static double Periodo(IEnumerable<(int Huevos, int Hembras)> dias)
    {
        long huevos = 0, avesDia = 0;
        foreach (var (h, hembras) in dias)
        {
            if (hembras <= 0) continue;
            huevos  += h;
            avesDia += hembras;
        }
        return avesDia > 0 ? (double)huevos / avesDia * 100d : 0d;
    }
}
