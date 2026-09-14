// src/ZooSanMarino.Application/Calculos/SeguimientoDiarioLevanteCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// ESPECIFICACIÓN EJECUTABLE de <c>fn_seguimiento_diario_levante</c> (BD): la fn SQL es la
/// dueña de la grilla diaria de levante y esta clase es su contrato en C# para la rama
/// <c>seg_dias_agrupado</c> (regla del repo «una sola fórmula por número»).
///
/// Reglas de agregación cuando <c>companies.permite_multiples_seguimientos_diarios</c> está ON
/// (plan seguimiento_produccion_multiples_registros_dia, §5/S6; ajustadas en v2 por el plan
/// levante_varios_registros_dia_pesaje_uniformidad, mismo criterio que producción v4):
///  • Aditivos (mortalidad, selección, error de sexaje, consumo, traslados, venta) → SUMA.
///  • Peso promedio, kcal y proteína → PROMEDIO de los registros que MIDIERON (&gt; 0); si ninguno
///    midió, el promedio de siempre ignorando nulos. Ponderar por aves vivas equivale a esto: son
///    un valor de DÍA constante entre los registros del mismo día.
///  • Uniformidad y CV → el ÚLTIMO registro que la trae (un nulo no tapa la medición del día).
///  • «Último» = mayor timestamp y, a igual timestamp, mayor id (los forms graban a mediodía).
/// Con UN solo registro el día, cada regla da exactamente el valor de esa fila — mismo
/// resultado que sin agrupar. Sin EF ni estado: funciones puras.
/// </summary>
public static class SeguimientoDiarioLevanteCalculos
{
    /// <summary>Registro crudo del día — subconjunto de columnas de <c>seguimiento_diario_levante</c>
    /// relevante para <see cref="AgruparPorDia"/>.</summary>
    public sealed record RegistroCrudo(
        long? RegId,
        int MortH, int MortM, int SelH, int SelM, int ErrH, int ErrM,
        double ConsKgH, double ConsKgM,
        int TrasSalH, int TrasSalM, int TrasIngH, int TrasIngM,
        int VentaH, int VentaM,
        double? PesoH, double? PesoM,
        double? UnifH, double? UnifM,
        double? CvH = null, double? CvM = null,
        double? KcalH = null, double? ProtH = null);

    /// <summary>
    /// Agrupa por día calendario — espejo de <c>seg_dias_agrupado</c> en
    /// <c>fn_seguimiento_diario_levante.sql</c> (v2).
    /// </summary>
    public static IReadOnlyList<(DateOnly Dia, RegistroCrudo Fila)> AgruparPorDia(
        IEnumerable<(DateOnly Dia, DateTime Ts, RegistroCrudo Fila)> filas)
        => filas
            .GroupBy(f => f.Dia)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                // ≙ ORDER BY c_ts, c_id. En Postgres un id NULL va PRIMERO en orden DESC (cuenta como
                // «el último»): acá va al final del orden ascendente. En levante c_id es la PK, nunca NULL.
                var ordenadas = g.OrderBy(f => f.Ts).ThenBy(f => f.Fila.RegId ?? long.MaxValue).ToList();
                var agregada = new RegistroCrudo(
                    RegId: ordenadas.Select(f => f.Fila.RegId).Where(id => id.HasValue).DefaultIfEmpty().Min(),
                    MortH: ordenadas.Sum(f => f.Fila.MortH),
                    MortM: ordenadas.Sum(f => f.Fila.MortM),
                    SelH: ordenadas.Sum(f => f.Fila.SelH),
                    SelM: ordenadas.Sum(f => f.Fila.SelM),
                    ErrH: ordenadas.Sum(f => f.Fila.ErrH),
                    ErrM: ordenadas.Sum(f => f.Fila.ErrM),
                    ConsKgH: ordenadas.Sum(f => f.Fila.ConsKgH),
                    ConsKgM: ordenadas.Sum(f => f.Fila.ConsKgM),
                    TrasSalH: ordenadas.Sum(f => f.Fila.TrasSalH),
                    TrasSalM: ordenadas.Sum(f => f.Fila.TrasSalM),
                    TrasIngH: ordenadas.Sum(f => f.Fila.TrasIngH),
                    TrasIngM: ordenadas.Sum(f => f.Fila.TrasIngM),
                    VentaH: ordenadas.Sum(f => f.Fila.VentaH),
                    VentaM: ordenadas.Sum(f => f.Fila.VentaM),
                    PesoH: PromedioDeLosQueMidieron(ordenadas.Select(f => f.Fila.PesoH)),
                    PesoM: PromedioDeLosQueMidieron(ordenadas.Select(f => f.Fila.PesoM)),
                    UnifH: UltimoNoNulo(ordenadas.Select(f => f.Fila.UnifH)),
                    UnifM: UltimoNoNulo(ordenadas.Select(f => f.Fila.UnifM)),
                    CvH: UltimoNoNulo(ordenadas.Select(f => f.Fila.CvH)),
                    CvM: UltimoNoNulo(ordenadas.Select(f => f.Fila.CvM)),
                    KcalH: PromedioDeLosQueMidieron(ordenadas.Select(f => f.Fila.KcalH)),
                    ProtH: PromedioDeLosQueMidieron(ordenadas.Select(f => f.Fila.ProtH)));
                return (g.Key, agregada);
            })
            .ToList();

    /// <summary>Cuenta días calendario DISTINTOS, no filas — espejo de
    /// <c>COUNT(DISTINCT reg_date)</c> en las fns semanales de levante (antes <c>COUNT(*)</c>,
    /// que sobre-contaba con 2+ registros el mismo día).</summary>
    public static int ContarDias(IEnumerable<DateOnly> fechas) => fechas.Distinct().Count();

    /// <summary>
    /// ≙ <c>COALESCE(AVG(x) FILTER (WHERE x &gt; 0), AVG(x))</c>: promedio de los registros que midieron;
    /// si ninguno midió, el AVG de siempre ignorando nulos (0 si traían 0, null si no traían nada).
    /// </summary>
    private static double? PromedioDeLosQueMidieron(IEnumerable<double?> valores)
    {
        var lista = valores.ToList();
        var positivos = lista.Where(v => v > 0).Select(v => v!.Value).ToList();
        if (positivos.Count > 0) return positivos.Average();
        var noNulos = lista.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return noNulos.Count == 0 ? null : noNulos.Average();
    }

    /// <summary>
    /// ≙ <c>(array_agg(x ORDER BY c_ts DESC, c_id DESC) FILTER (WHERE x IS NOT NULL))[1]</c> sobre la
    /// serie ya ordenada: el último valor medido del día, o null si ningún registro lo trae.
    /// </summary>
    private static double? UltimoNoNulo(IEnumerable<double?> valores) => valores.LastOrDefault(v => v.HasValue);
}
