// Cálculo PURO del merge dual-fuente de la sección "Movimientos de Huevos" del Reporte Contable.
// Sin EF, sin estado, sin I/O: combina las filas de la tabla legacy (seguimiento_diario_levante,
// tipo 'produccion') con las de seguimiento_diario_produccion.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Merge de los seguimientos de producción entre la fuente legacy y la canónica, con el mismo
/// criterio que <c>fn_indicadores_produccion_postura</c> (Migrations/20260728160000): UNION de
/// ambas fuentes y, por cada día calendario, gana el registro de timestamp más temprano
/// (<c>DISTINCT ON (día) ORDER BY día, ts</c>). Aquí el reporte consolida VARIOS lotes, por lo
/// que la unidad de dedup es (lote, día).
/// </summary>
public static class ReporteContableHuevosCalculos
{
    /// <summary>
    /// Fila diaria de huevos de un lote, normalizada desde cualquiera de las dos fuentes.
    /// <c>Fecha</c> conserva el timestamp completo (decide el ganador del día);
    /// <c>EsLegacy</c> desempata a favor de la fila legacy ante timestamps idénticos
    /// (la fn no define orden en ese empate; acá se fija uno determinista).
    /// </summary>
    public readonly record struct FilaHuevosDia(
        int LoteId,
        DateTime Fecha,
        bool EsLegacy,
        int HuevoTot,
        int HuevoInc,
        int HuevoLimpio,
        int HuevoTratado,
        int HuevoSucio,
        int HuevoDeforme,
        int HuevoBlanco,
        int HuevoDobleYema,
        int HuevoPiso,
        int HuevoPequeno,
        int HuevoRoto,
        int HuevoDesecho,
        int HuevoOtro);

    /// <summary>
    /// Deduplica por (lote, día calendario) quedándose con el registro de timestamp más temprano
    /// (empate exacto → legacy). Devuelve las filas ordenadas por Fecha y LoteId, el mismo orden
    /// que producía la consulta legacy original del reporte.
    /// </summary>
    public static List<FilaHuevosDia> MergeDualFuentePorDia(IEnumerable<FilaHuevosDia> filas) =>
        filas
            .GroupBy(f => (f.LoteId, Dia: f.Fecha.Date))
            .Select(g => g
                .OrderBy(f => f.Fecha)
                .ThenByDescending(f => f.EsLegacy)
                .First())
            .OrderBy(f => f.Fecha)
            .ThenBy(f => f.LoteId)
            .ToList();

    /// <summary>
    /// Menor de dos fechas donde <c>default</c> significa "fuente sin filas" (el
    /// <c>FirstOrDefaultAsync</c> de cada consulta). Ambas vacías → <c>default</c>.
    /// </summary>
    public static DateTime MenorFechaNoDefault(DateTime a, DateTime b) =>
        a == default ? b
        : b == default ? a
        : a <= b ? a : b;

    /// <summary>Mayor de dos fechas con la misma semántica de <c>default</c> que
    /// <see cref="MenorFechaNoDefault"/>.</summary>
    public static DateTime MayorFechaNoDefault(DateTime a, DateTime b) =>
        a == default ? b
        : b == default ? a
        : a >= b ? a : b;
}
