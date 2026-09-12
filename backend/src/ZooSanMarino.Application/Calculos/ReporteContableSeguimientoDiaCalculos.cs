namespace ZooSanMarino.Application.Calculos;

/// <summary>Registro de seguimiento de levante tal como lo lee el reporte contable.</summary>
public sealed record SeguimientoLevanteContableFila(
    int LoteId,
    DateTime Fecha,
    int? MortalidadHembras,
    int? MortalidadMachos,
    int? SelH,
    int? SelM,
    decimal? ConsumoKgHembras,
    decimal? ConsumoKgMachos);

/// <summary>Registro de seguimiento de producción tal como lo lee el reporte contable.</summary>
public sealed record SeguimientoProduccionContableFila(
    int LoteId,
    DateTime Fecha,
    int MortalidadH,
    int MortalidadM,
    int SelH,
    decimal ConsKgH,
    decimal ConsKgM);

/// <summary>
/// El reporte contable arma UNA fila por lote y por día, y la buscaba con
/// <c>FirstOrDefault(lote, fecha)</c> sobre los registros crudos. Con
/// <c>permite_multiples_seguimientos_diarios</c> un día puede tener varios registros, y el reporte
/// mostraba solo uno — el que devolviera la base —. Estas funciones consolidan el día ANTES de esa
/// búsqueda.
///
/// <para>
/// Con un registro por día devuelven exactamente ese registro, así que el reporte de una empresa sin
/// duplicados no cambia. En levante los valores son nullables: si TODOS los registros del día traen
/// <c>null</c> en un campo, el agregado queda <c>null</c>, porque el reporte usa
/// <c>levante?.MortalidadHembras ?? produccion?.MortalidadH</c> y un 0 inventado taparía el valor de
/// producción el día de la transición.
/// </para>
/// </summary>
public static class ReporteContableSeguimientoDiaCalculos
{
    /// <summary>Un registro por (lote, día calendario); los aditivos se suman.</summary>
    public static List<SeguimientoLevanteContableFila> AgruparLevantePorLoteDia(
        IEnumerable<SeguimientoLevanteContableFila> filas) =>
        filas
            .GroupBy(f => (f.LoteId, Dia: f.Fecha.Date))
            .Select(g => g.Count() == 1
                ? g.First()
                : new SeguimientoLevanteContableFila(
                    g.Key.LoteId,
                    g.Min(f => f.Fecha),
                    SumaOpcional(g.Select(f => f.MortalidadHembras)),
                    SumaOpcional(g.Select(f => f.MortalidadMachos)),
                    SumaOpcional(g.Select(f => f.SelH)),
                    SumaOpcional(g.Select(f => f.SelM)),
                    SumaOpcional(g.Select(f => f.ConsumoKgHembras)),
                    SumaOpcional(g.Select(f => f.ConsumoKgMachos))))
            .ToList();

    /// <summary>Un registro por (lote, día calendario); los aditivos se suman.</summary>
    public static List<SeguimientoProduccionContableFila> AgruparProduccionPorLoteDia(
        IEnumerable<SeguimientoProduccionContableFila> filas) =>
        filas
            .GroupBy(f => (f.LoteId, Dia: f.Fecha.Date))
            .Select(g => g.Count() == 1
                ? g.First()
                : new SeguimientoProduccionContableFila(
                    g.Key.LoteId,
                    g.Min(f => f.Fecha),
                    g.Sum(f => f.MortalidadH),
                    g.Sum(f => f.MortalidadM),
                    g.Sum(f => f.SelH),
                    g.Sum(f => f.ConsKgH),
                    g.Sum(f => f.ConsKgM)))
            .ToList();

    /// <summary>Suma los valores presentes; <c>null</c> solo si ninguno lo está.</summary>
    public static int? SumaOpcional(IEnumerable<int?> valores)
    {
        int? total = null;
        foreach (var v in valores)
            if (v.HasValue) total = (total ?? 0) + v.Value;
        return total;
    }

    /// <summary>Suma los valores presentes; <c>null</c> solo si ninguno lo está.</summary>
    public static decimal? SumaOpcional(IEnumerable<decimal?> valores)
    {
        decimal? total = null;
        foreach (var v in valores)
            if (v.HasValue) total = (total ?? 0m) + v.Value;
        return total;
    }
}
