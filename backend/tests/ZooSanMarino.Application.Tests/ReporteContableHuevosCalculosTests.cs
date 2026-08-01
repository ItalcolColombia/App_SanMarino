using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.ReporteContableHuevosCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Merge dual-fuente de la sección "Movimientos de Huevos" del Reporte Contable
/// (plan <c>fase_de_desarrollo/reporte_contable_movimientos_huevos_dual_fuente_plan.md</c>).
/// Criterio canónico de <c>fn_indicadores_produccion_postura</c>: UNION de la fuente legacy
/// (seguimiento_diario_levante tipo 'produccion') con seguimiento_diario_produccion y, por cada
/// (lote, día calendario), gana el registro de timestamp más temprano; empate exacto → legacy.
/// </summary>
public class ReporteContableHuevosCalculosTests
{
    private static FilaHuevosDia Fila(int lote, DateTime ts, bool legacy, int tot) => new(
        LoteId: lote, Fecha: ts, EsLegacy: legacy,
        HuevoTot: tot, HuevoInc: tot + 1, HuevoLimpio: tot + 2, HuevoTratado: tot + 3,
        HuevoSucio: tot + 4, HuevoDeforme: tot + 5, HuevoBlanco: tot + 6, HuevoDobleYema: tot + 7,
        HuevoPiso: tot + 8, HuevoPequeno: tot + 9, HuevoRoto: tot + 10, HuevoDesecho: tot + 11,
        HuevoOtro: tot + 12);

    private static readonly DateTime D1 = new(2026, 6, 8, 12, 0, 0);
    private static readonly DateTime D2 = new(2026, 6, 9, 12, 0, 0);
    private static readonly DateTime D3 = new(2026, 6, 10, 12, 0, 0);

    [Fact]
    public void SoloLegacy_PassthroughCompleto()
    {
        var legacy = new[] { Fila(14, D1, true, 100), Fila(14, D2, true, 200), Fila(14, D3, true, 300) };

        var merged = MergeDualFuentePorDia(legacy);

        Assert.Equal(legacy, merged); // mismas filas, mismos valores, mismo orden
    }

    [Fact]
    public void SoloNueva_PassthroughCompleto()
    {
        var nueva = new[] { Fila(130, D1, false, 3730), Fila(130, D2, false, 3600) };

        var merged = MergeDualFuentePorDia(nueva);

        Assert.Equal(nueva, merged);
    }

    [Fact]
    public void MismoLoteYDia_GanaElTimestampMasTemprano_NuevaMasTempranaGana()
    {
        // Arrastre del cierre a las 07:00 (fuente nueva) vs fila legacy del mismo día a las 12:00.
        var filas = new[]
        {
            Fila(130, D1, legacy: true, tot: 999),
            Fila(130, new DateTime(2026, 6, 8, 7, 0, 0), legacy: false, tot: 130)
        };

        var merged = MergeDualFuentePorDia(filas);

        var unica = Assert.Single(merged);
        Assert.False(unica.EsLegacy);
        Assert.Equal(130, unica.HuevoTot);
    }

    [Fact]
    public void MismoLoteYDia_GanaElTimestampMasTemprano_LegacyMasTempranaGana()
    {
        var filas = new[]
        {
            Fila(14, new DateTime(2026, 6, 8, 6, 30, 0), legacy: true, tot: 500),
            Fila(14, D1, legacy: false, tot: 999)
        };

        var merged = MergeDualFuentePorDia(filas);

        var unica = Assert.Single(merged);
        Assert.True(unica.EsLegacy);
        Assert.Equal(500, unica.HuevoTot);
    }

    [Fact]
    public void EmpateExactoDeTimestamp_GanaLegacy()
    {
        // Da igual el orden de llegada: el desempate es por EsLegacy, no por posición.
        var filas = new[]
        {
            Fila(14, D1, legacy: false, tot: 999),
            Fila(14, D1, legacy: true, tot: 500)
        };

        var merged = MergeDualFuentePorDia(filas);

        var unica = Assert.Single(merged);
        Assert.True(unica.EsLegacy);
        Assert.Equal(500, unica.HuevoTot);
    }

    [Fact]
    public void LotesDistintosElMismoDia_NoSePisan()
    {
        // El dedup es por (lote, día): el reporte luego SUMA los lotes de cada día.
        var filas = new[]
        {
            Fila(13, D1, legacy: false, tot: 100),
            Fila(14, D1, legacy: false, tot: 200)
        };

        var merged = MergeDualFuentePorDia(filas);

        Assert.Equal(2, merged.Count);
        Assert.Equal(new[] { 13, 14 }, merged.Select(f => f.LoteId).ToArray());
    }

    [Fact]
    public void DuplicadoDentroDeLaMismaFuente_ColapsaAlMasTemprano()
    {
        // Semántica DISTINCT ON de la fn: un día nunca aporta más de una fila por lote.
        var filas = new[]
        {
            Fila(14, new DateTime(2026, 6, 8, 15, 0, 0), legacy: false, tot: 999),
            Fila(14, new DateTime(2026, 6, 8, 9, 0, 0), legacy: false, tot: 100)
        };

        var merged = MergeDualFuentePorDia(filas);

        var unica = Assert.Single(merged);
        Assert.Equal(100, unica.HuevoTot);
    }

    [Fact]
    public void DiasSinSolape_SeUnenDeAmbasFuentes_OrdenFechaLuegoLote()
    {
        var filas = new[]
        {
            Fila(14, D3, legacy: false, tot: 300),
            Fila(13, D3, legacy: false, tot: 250),
            Fila(14, D1, legacy: true, tot: 100),
            Fila(14, D2, legacy: false, tot: 200)
        };

        var merged = MergeDualFuentePorDia(filas);

        Assert.Equal(4, merged.Count);
        Assert.Equal(new[] { (14, 100), (14, 200), (13, 250), (14, 300) },
            merged.Select(f => (f.LoteId, f.HuevoTot)).ToArray());
    }

    [Theory]
    // default = fuente sin filas (FirstOrDefaultAsync): la otra fecha manda; ambas vacías → default.
    [InlineData("2026-06-08", "2026-06-10", "2026-06-08", "2026-06-10")]
    [InlineData("2026-06-10", "2026-06-08", "2026-06-08", "2026-06-10")]
    [InlineData("2026-06-08", null, "2026-06-08", "2026-06-08")]
    [InlineData(null, "2026-06-10", "2026-06-10", "2026-06-10")]
    [InlineData(null, null, null, null)]
    public void MenorYMayorFechaNoDefault_IgnoranElDefault(
        string? a, string? b, string? menorEsperado, string? mayorEsperado)
    {
        var fa = a is null ? default : DateTime.Parse(a);
        var fb = b is null ? default : DateTime.Parse(b);

        Assert.Equal(menorEsperado is null ? default : DateTime.Parse(menorEsperado),
            MenorFechaNoDefault(fa, fb));
        Assert.Equal(mayorEsperado is null ? default : DateTime.Parse(mayorEsperado),
            MayorFechaNoDefault(fa, fb));
    }
}
