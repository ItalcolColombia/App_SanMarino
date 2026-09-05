// src/ZooSanMarino.Application/Calculos/SeguimientoDiarioLevanteCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// ESPECIFICACIÓN EJECUTABLE de <c>fn_seguimiento_diario_levante</c> (BD): la fn SQL es la
/// dueña de la grilla diaria de levante y esta clase es su contrato en C# para la rama
/// <c>seg_dias_agrupado</c> (regla del repo «una sola fórmula por número»).
///
/// Reglas de agregación cuando <c>companies.permite_multiples_seguimientos_diarios</c> está ON
/// (plan seguimiento_produccion_multiples_registros_dia, §5/S6):
///  • Aditivos (mortalidad, selección, error de sexaje, consumo, traslados, venta) → SUMA.
///  • Peso promedio → PROMEDIO simple (equivale a ponderar por aves vivas, un valor de DÍA
///    constante entre los registros del mismo día).
///  • Uniformidad y CV → gana el ÚLTIMO registro del día (no se promedia — medición puntual).
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
        double? UnifH, double? UnifM);

    /// <summary>
    /// Agrupa por día calendario — espejo de <c>seg_dias_agrupado</c> en
    /// <c>fn_seguimiento_diario_levante.sql</c>.
    /// </summary>
    public static IReadOnlyList<(DateOnly Dia, RegistroCrudo Fila)> AgruparPorDia(
        IEnumerable<(DateOnly Dia, DateTime Ts, RegistroCrudo Fila)> filas)
        => filas
            .GroupBy(f => f.Dia)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var ordenadas = g.OrderBy(f => f.Ts).ToList();
                var ultima = ordenadas[^1].Fila;
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
                    PesoH: PromedioONulo(ordenadas.Select(f => f.Fila.PesoH)),
                    PesoM: PromedioONulo(ordenadas.Select(f => f.Fila.PesoM)),
                    UnifH: ultima.UnifH,
                    UnifM: ultima.UnifM);
                return (g.Key, agregada);
            })
            .ToList();

    /// <summary>Cuenta días calendario DISTINTOS, no filas — espejo de
    /// <c>COUNT(DISTINCT reg_date)</c> en las fns semanales de levante (antes <c>COUNT(*)</c>,
    /// que sobre-contaba con 2+ registros el mismo día).</summary>
    public static int ContarDias(IEnumerable<DateOnly> fechas) => fechas.Distinct().Count();

    private static double? PromedioONulo(IEnumerable<double?> valores)
    {
        var noNulos = valores.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return noNulos.Count == 0 ? null : noNulos.Average();
    }
}
