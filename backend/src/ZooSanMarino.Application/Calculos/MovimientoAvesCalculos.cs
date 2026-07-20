namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculos puros (sin dependencias de infraestructura ni EF) del módulo Movimientos de Aves
/// (reproductoras): semana del lote desde el encasetamiento, discriminación de fase
/// Levante/Producción por semana y etapa de producción.
/// Extraídos del servicio para ser deterministas y testeables; la aritmética es idéntica a la
/// que vivía inline en <c>MovimientoAvesService</c> (división entera por 7 + 1, umbral de
/// semana 26 y tramos de etapa 26–33 / 34–50 / resto).
/// </summary>
public static class MovimientoAvesCalculos
{
    /// <summary>Semana a partir de la cual el lote se considera en producción.</summary>
    public const int SemanaInicioProduccion = 26;

    /// <summary>
    /// Semana del lote (1-based) a la <paramref name="fecha"/> indicada, contada desde el
    /// encasetamiento. Trunca ambas fechas a día y usa división entera por 7 (idéntico al inline).
    /// </summary>
    public static int SemanaDesdeEncaset(DateTime fecha, DateTime fechaEncaset)
    {
        var diasDesdeEncaset = (fecha.Date - fechaEncaset.Date).Days;
        return (diasDesdeEncaset / 7) + 1;
    }

    /// <summary>
    /// Variante usada para determinar la fase "a hoy": 0 cuando aún no han transcurrido días
    /// desde el encasetamiento, en otro caso la semana 1-based.
    /// </summary>
    public static int SemanaDesdeEncasetOCero(DateTime fecha, DateTime fechaEncaset)
    {
        var diasDesdeEncaset = (fecha.Date - fechaEncaset.Date).Days;
        return diasDesdeEncaset > 0 ? (diasDesdeEncaset / 7) + 1 : 0;
    }

    /// <summary>El lote está en levante mientras la semana sea menor a <see cref="SemanaInicioProduccion"/>.</summary>
    public static bool EstaEnLevante(int semana) => semana < SemanaInicioProduccion;

    /// <summary>El lote está en producción a partir de la semana <see cref="SemanaInicioProduccion"/>.</summary>
    public static bool EstaEnProduccion(int semana) => semana >= SemanaInicioProduccion;

    /// <summary>
    /// Etapa de producción (1, 2 ó 3) según el tramo de semanas: 26–33 → 1, 34–50 → 2, &gt;50 → 3.
    /// REQ-012c: una semana &lt; 26 (registro temprano / borde) cae en la etapa 1, no en la 3 — antes
    /// el "resto" englobaba las semanas tempranas y devolvía etapa 3 erróneamente.
    /// </summary>
    public static int EtapaProduccion(int semana) =>
        semana <= 33 ? 1 : (semana <= 50 ? 2 : 3);
}
