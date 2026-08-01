// tests/ZooSanMarino.Application.Tests/SeguimientoDiarioProduccionCalculosTests.cs
using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.SeguimientoDiarioProduccionCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de <c>fn_seguimiento_diario_produccion</c> (la fn SQL es la dueña; esta clase es el
/// test — regla «una sola fórmula por número»). El caso testigo es el E2E REAL del lote 130
/// (LOTE NIZA E2E, LPP 11): 9.495 H / 929 M iniciales, 7 días con mort 5/1 + sel 2/0 +
/// err 1/0, venta 200/20 (día 2), salida 300 (día 4), ingreso 100 (día 5) ⇒ 9.039 H / 902 M,
/// verificado contra la BD y el cuadre del tracker (commit 21a5c81).
/// </summary>
public class SeguimientoDiarioProduccionCalculosTests
{
    private static DateOnly D(int dia) => new(2026, 6, dia);

    // ── Caso testigo lote 130 ────────────────────────────────────────────────────────────

    private static (List<DiaSeguimiento?> Dias, Dictionary<DateOnly, MovimientosDia> Movs, List<DateOnly> Fechas) Lote130()
    {
        var fechas = Enumerable.Range(8, 7).Select(D).ToList();
        var dias = new List<DiaSeguimiento?>();
        long segId = 675;
        foreach (var f in fechas)
        {
            var huevoTot = f == D(8) ? 3730 : 3600;           // día 8 = merge del arrastre (3600 + 130)
            var huevoInc = f == D(8) ? 3630 : 3500;
            dias.Add(new DiaSeguimiento(f, segId++, MortH: 5, MortM: 1, SelH: 2, SelM: 0,
                ErrH: 1, ErrM: 0, HuevoTot: huevoTot, HuevoInc: huevoInc));
        }
        var movs = new Dictionary<DateOnly, MovimientosDia>
        {
            [D(9)] = new(D(9), OutH: 200, OutM: 20, InH: 0, InM: 0),    // venta 200/20
            [D(11)] = new(D(11), OutH: 300, OutM: 0, InH: 0, InM: 0),   // salida 300 -> lote 13
            [D(12)] = new(D(12), OutH: 0, OutM: 0, InH: 100, InM: 0),   // ingreso 100 <- lote 13
        };
        return (dias, movs, fechas);
    }

    [Fact]
    public void Lote130_SaldoFinal_9039H_902M_ConErrorDeSexaje()
    {
        var (dias, movs, fechas) = Lote130();
        var serie = CalcularSerie(9495, 929, dias, movs, fechas);

        Assert.Equal(7, serie.Count);
        Assert.Equal(9039, serie[^1].SaldoAvesH);   // 9495 − (35+14+7) − (200+300) + 100
        Assert.Equal(902, serie[^1].SaldoAvesM);    // 929 − 7 − 20
        Assert.Equal(25330, serie[^1].HuevoTotAcum);
        Assert.Equal(24630, serie[^1].HuevoIncAcum);
    }

    [Fact]
    public void Lote130_InicioDelDia_ExcluyeBajasYMovimientosDelMismoDia()
    {
        var (dias, movs, fechas) = Lote130();
        var serie = CalcularSerie(9495, 929, dias, movs, fechas);

        Assert.Equal(9495, serie[0].AvesHInicioDia);            // día 1: nada previo
        Assert.Equal(9487, serie[1].AvesHInicioDia);            // 9495 − 8 bajas del día 1
        Assert.Equal(9279, serie[2].AvesHInicioDia);            // tras venta 200 + 8 bajas del día 2
        Assert.Equal(8963, serie[4].AvesHInicioDia);            // tras salida 300 del día 4
        Assert.Equal(9055, serie[5].AvesHInicioDia);            // tras ingreso 100 del día 5
    }

    [Fact]
    public void Lote130_PctPostura_HenDayDiario_SobreInicioDelDia()
    {
        var (dias, movs, fechas) = Lote130();
        var serie = CalcularSerie(9495, 929, dias, movs, fechas);

        Assert.Equal(100.0 * 3730 / 9495, serie[0].PctPosturaDia!.Value, 10);
        Assert.Equal(100.0 * 3600 / 9487, serie[1].PctPosturaDia!.Value, 10);
    }

    [Fact]
    public void MovimientoSinFilaDeSeguimiento_GeneraDiaEnLaSerie_YAfectaElSaldo()
    {
        // Venta tardía sin registro diario (patrón engorde v7: la venta genera su fila)
        var fechas = new List<DateOnly> { D(1), D(3) };
        var dias = new List<DiaSeguimiento?>
        {
            new(D(1), 10, MortH: 5, MortM: 0, SelH: 0, SelM: 0, ErrH: 0, ErrM: 0, HuevoTot: 100, HuevoInc: 90),
            null, // día solo-movimiento
        };
        var movs = new Dictionary<DateOnly, MovimientosDia> { [D(3)] = new(D(3), 50, 0, 0, 0) };

        var serie = CalcularSerie(1000, 100, dias, movs, fechas);

        Assert.Null(serie[1].SegId);
        Assert.Equal(945, serie[1].SaldoAvesH);   // 1000 − 5 − 50
        Assert.Equal(100, serie[1].HuevoTotAcum); // el día sin registro no suma huevos
        Assert.Equal(0, serie[1].PctPosturaDia);  // 0 huevos ese día
    }

    [Fact]
    public void SaldoNuncaNegativo_ClampCero()
    {
        var fechas = new List<DateOnly> { D(1) };
        var dias = new List<DiaSeguimiento?>
        {
            new(D(1), 1, MortH: 80, MortM: 0, SelH: 30, SelM: 0, ErrH: 5, ErrM: 0, HuevoTot: 0, HuevoInc: 0),
        };
        var serie = CalcularSerie(100, 10, dias, new Dictionary<DateOnly, MovimientosDia>(), fechas);

        Assert.Equal(0, serie[0].SaldoAvesH);     // 100 − 115 → clamp 0
        Assert.Equal(10, serie[0].SaldoAvesM);
    }

    [Fact]
    public void RamaLegacySinLpp_SaldosNull_NuncaCero()
    {
        // En SQL, GREATEST(0, NULL − x) devolvería 0 (GREATEST ignora NULLs): la fn usa CASE
        // y acá el contrato es NULL explícito.
        var fechas = new List<DateOnly> { D(1) };
        var dias = new List<DiaSeguimiento?>
        {
            new(D(1), 1, 5, 1, 0, 0, 0, 0, 454, 32),
        };
        var serie = CalcularSerie(baseH: null, baseM: null, dias, new Dictionary<DateOnly, MovimientosDia>(), fechas);

        Assert.Null(serie[0].SaldoAvesH);
        Assert.Null(serie[0].SaldoAvesM);
        Assert.Null(serie[0].AvesHInicioDia);
        Assert.Null(serie[0].PctPosturaDia);
        Assert.Equal(454, serie[0].HuevoTotAcum); // los huevos sí acumulan sin base de aves
    }

    [Fact]
    public void PctPostura_CeroHembrasVivas_DevuelveCero_ComoLaFnSemanal()
    {
        var fechas = new List<DateOnly> { D(1), D(2) };
        var dias = new List<DiaSeguimiento?>
        {
            new(D(1), 1, MortH: 10, MortM: 0, SelH: 0, SelM: 0, ErrH: 0, ErrM: 0, HuevoTot: 5, HuevoInc: 5),
            new(D(2), 2, 0, 0, 0, 0, 0, 0, HuevoTot: 3, HuevoInc: 3),
        };
        var serie = CalcularSerie(10, 0, dias, new Dictionary<DateOnly, MovimientosDia>(), fechas);

        Assert.Equal(0, serie[1].AvesHInicioDia);
        Assert.Equal(0d, serie[1].PctPosturaDia);
    }

    // ── Dedup y semana ───────────────────────────────────────────────────────────────────

    [Fact]
    public void DedupPorDia_GanaElTimestampMasTemprano()
    {
        var dia = D(10);
        var filas = new[]
        {
            (dia, new DateTime(2026, 6, 10, 17, 0, 0, DateTimeKind.Utc), "tarde"),
            (dia, new DateTime(2026, 6, 10, 5, 0, 0, DateTimeKind.Utc), "temprano"),
            (D(11), new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc), "otro-dia"),
        };

        var dedup = DedupPorDia(filas);

        Assert.Equal(new[] { "temprano", "otro-dia" }, dedup);
    }

    [Theory]
    [InlineData(0, 1)]   // el mismo día de la referencia = semana 1
    [InlineData(6, 1)]
    [InlineData(7, 2)]   // división entera, no ceil
    [InlineData(13, 2)]
    [InlineData(174, 25)]
    [InlineData(175, 26)]
    public void SemanaVida_DivisionEntera_SinPisoNiCorte(int dias, int esperada)
    {
        var refDate = new DateOnly(2026, 6, 1);
        Assert.Equal(esperada, SemanaVida(refDate.AddDays(dias), refDate));
    }

    [Fact]
    public void EdadDias_ClampCero_YSemanaCrudaTruncaHaciaCero()
    {
        var refDate = new DateOnly(2026, 6, 10);
        Assert.Equal(0, EdadDias(refDate.AddDays(-3), refDate));   // GREATEST(0, ...)
        // División entera truncando hacia 0 (idéntico C# y Postgres): (−3/7)+1 = 0+1 = 1
        Assert.Equal(1, SemanaVida(refDate.AddDays(-3), refDate));
        Assert.Equal(0, SemanaVida(refDate.AddDays(-8), refDate)); // (−8/7)+1 = −1+1 = 0
    }

    [Fact]
    public void CalcularSerie_UniversoDesalineado_Lanza()
    {
        Assert.Throws<ArgumentException>(() => CalcularSerie(
            10, 10,
            new List<DiaSeguimiento?> { null },
            new Dictionary<DateOnly, MovimientosDia>(),
            new List<DateOnly> { D(1), D(2) }));
    }
}
