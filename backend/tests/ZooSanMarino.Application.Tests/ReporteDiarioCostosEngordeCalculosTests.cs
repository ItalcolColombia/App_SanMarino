// tests/ZooSanMarino.Application.Tests/ReporteDiarioCostosEngordeCalculosTests.cs
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.ReporteDiarioCostosEngorde;
using Xunit;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Totales del footer y aves vivas actuales del Reporte Diario Costos engorde
/// (consolidación pura sobre filas ya agregadas por fn_reporte_diario_costos_engorde).
/// </summary>
public class ReporteDiarioCostosEngordeCalculosTests
{
    private static ReporteDiarioCostosGalponDiaDto Galpon(
        string id, string nombre, int mort, int sel, int err, double consumo, int aves) =>
        new(id, nombre, mort, sel, err, mort + sel, consumo, aves);

    private static ReporteDiarioCostosFilaDto Fila(
        DateTime fecha,
        double consumoTotal,
        IReadOnlyList<ReporteDiarioCostosAlimentoDto> alimentos,
        IReadOnlyList<ReporteDiarioCostosGalponDiaDto> galpones) =>
        new(
            fecha,
            consumoTotal,
            galpones.Sum(g => g.MortSel),
            galpones.Sum(g => g.AvesVivas),
            alimentos,
            galpones);

    // ─────────────────────────────── ConstruirTotales ───────────────────────────────

    [Fact]
    public void ConstruirTotales_SinFilas_DevuelveCerosSinExcepcion()
    {
        var tot = ReporteDiarioCostosEngordeCalculos.ConstruirTotales(Array.Empty<ReporteDiarioCostosFilaDto>());

        Assert.Equal(0, tot.ConsumoTotalKg);
        Assert.Equal(0, tot.MortSelTotal);
        Assert.Empty(tot.Alimentos);
        Assert.Empty(tot.PorGalpon);
    }

    [Fact]
    public void ConstruirTotales_SumaConsumoGlobalYMortSel_ComoElMockup()
    {
        // Día 1 del mockup: Alimento a 34kg + alimento b 5kg = 39kg
        var filas = new[]
        {
            Fila(new DateTime(2026, 7, 1), 39,
                new[]
                {
                    new ReporteDiarioCostosAlimentoDto("Alimento a", 2000, 34),
                    new ReporteDiarioCostosAlimentoDto("alimento b", 5000, 5)
                },
                new[]
                {
                    Galpon("G1", "Galpón 1", 2, 1, 0, 20, 4),
                    Galpon("G2", "Galpón 2", 3, 0, 1, 19, 136)
                }),
            Fila(new DateTime(2026, 7, 2), 41,
                new[] { new ReporteDiarioCostosAlimentoDto("Alimento a", 1959, 41) },
                new[]
                {
                    Galpon("G1", "Galpón 1", 1, 0, 0, 21, 3),
                    Galpon("G2", "Galpón 2", 0, 2, 0, 20, 134)
                })
        };

        var tot = ReporteDiarioCostosEngordeCalculos.ConstruirTotales(filas);

        Assert.Equal(80, tot.ConsumoTotalKg);          // 39 + 41
        Assert.Equal(9, tot.MortSelTotal);             // (3+3) + (1+2)
    }

    [Fact]
    public void ConstruirTotales_AgrupaAlimentosPorNombre_CaseInsensitive_YOrdena()
    {
        var filas = new[]
        {
            Fila(new DateTime(2026, 7, 1), 10,
                new[] { new ReporteDiarioCostosAlimentoDto("Iniciador", 100, 6), new ReporteDiarioCostosAlimentoDto("Engorde", 50, 4) },
                new[] { Galpon("G1", "Galpón 1", 0, 0, 0, 10, 10) }),
            Fila(new DateTime(2026, 7, 2), 7,
                new[] { new ReporteDiarioCostosAlimentoDto("INICIADOR", 94, 7) },
                new[] { Galpon("G1", "Galpón 1", 0, 0, 0, 7, 10) })
        };

        var tot = ReporteDiarioCostosEngordeCalculos.ConstruirTotales(filas);

        Assert.Equal(2, tot.Alimentos.Count);
        Assert.Equal("Engorde", tot.Alimentos[0].NombreAlimento);   // orden alfabético
        Assert.Equal(4, tot.Alimentos[0].ConsumoKg);
        Assert.Equal("Iniciador", tot.Alimentos[1].NombreAlimento); // merge case-insensitive
        Assert.Equal(13, tot.Alimentos[1].ConsumoKg);               // 6 + 7
    }

    [Fact]
    public void ConstruirTotales_SumaPorGalpon_MortSelYErrSexaje()
    {
        var filas = new[]
        {
            Fila(new DateTime(2026, 7, 1), 0,
                Array.Empty<ReporteDiarioCostosAlimentoDto>(),
                new[] { Galpon("G1", "Galpón 1", 2, 1, 1, 0, 50), Galpon("G2", "Galpón 2", 0, 0, 0, 0, 80) }),
            Fila(new DateTime(2026, 7, 2), 0,
                Array.Empty<ReporteDiarioCostosAlimentoDto>(),
                new[] { Galpon("G1", "Galpón 1", 3, 2, 0, 0, 45), Galpon("G2", "Galpón 2", 1, 1, 2, 0, 78) })
        };

        var tot = ReporteDiarioCostosEngordeCalculos.ConstruirTotales(filas);

        var g1 = Assert.Single(tot.PorGalpon, g => g.GalponId == "G1");
        Assert.Equal(5, g1.Mortalidad);
        Assert.Equal(3, g1.Seleccion);
        Assert.Equal(1, g1.ErrSexaje);
        Assert.Equal(8, g1.MortSel);   // "SUMA TOTAL DEL GALPÓN"

        var g2 = Assert.Single(tot.PorGalpon, g => g.GalponId == "G2");
        Assert.Equal(1, g2.Mortalidad);
        Assert.Equal(1, g2.Seleccion);
        Assert.Equal(2, g2.MortSel);
    }

    // ───────────────────── Desglose por sexo (fn v4, pedido de Ecuador) ─────────────────────

    /// <summary>Galpón con mortalidad/selección por sexo; los combinados salen de sumar H + M (como la fn).</summary>
    private static ReporteDiarioCostosGalponDiaDto GalponPorSexo(
        string id, int mortH, int mortM, int selH, int selM, int aves) =>
        new(id, "Galpón " + id, mortH + mortM, selH + selM, 0, mortH + mortM + selH + selM, 0, aves,
            MortalidadHembras: mortH, MortalidadMachos: mortM,
            SeleccionHembras: selH, SeleccionMachos: selM,
            MortSelHembras: mortH + selH, MortSelMachos: mortM + selM);

    [Fact]
    public void ConstruirTotales_SumaPorGalpon_DesglosePorSexo_YHmasMEsElCombinado()
    {
        // Kilometro 22 / lote base 2604, Galpon-2 los días 27 y 28-ago (medido en la fn v4):
        //   27-ago: mort H 15 · M 21, sel 0      → 36
        //   28-ago: mort H 4 · M 10, sel H 10 · M 15 → 39
        var filas = new[]
        {
            Fila(new DateTime(2026, 8, 27), 0, Array.Empty<ReporteDiarioCostosAlimentoDto>(),
                new[] { GalponPorSexo("G0036", 15, 21, 0, 0, 47338) }),
            Fila(new DateTime(2026, 8, 28), 0, Array.Empty<ReporteDiarioCostosAlimentoDto>(),
                new[] { GalponPorSexo("G0036", 4, 10, 10, 15, 47299) })
        };

        var g = Assert.Single(ReporteDiarioCostosEngordeCalculos.ConstruirTotales(filas).PorGalpon);

        Assert.Equal(19, g.MortalidadHembras);   // 15 + 4
        Assert.Equal(31, g.MortalidadMachos);    // 21 + 10
        Assert.Equal(10, g.SeleccionHembras);
        Assert.Equal(15, g.SeleccionMachos);
        Assert.Equal(29, g.MortSelHembras);      // 19 + 10
        Assert.Equal(46, g.MortSelMachos);       // 31 + 15
        Assert.Equal(75, g.MortSel);             // 36 + 39: el número de siempre
        Assert.Equal(g.MortSel, g.MortSelHembras + g.MortSelMachos);
        Assert.Equal(g.Mortalidad, g.MortalidadHembras + g.MortalidadMachos);
        Assert.Equal(g.Seleccion, g.SeleccionHembras + g.SeleccionMachos);
    }

    [Fact]
    public void ConstruirTotales_FilasSinDesglose_CombinadosIntactosYSexoEnCero()
    {
        // Filas como las de la fn v3 (sin las claves por sexo): los combinados no cambian.
        var filas = new[]
        {
            Fila(new DateTime(2026, 7, 1), 0, Array.Empty<ReporteDiarioCostosAlimentoDto>(),
                new[] { Galpon("G1", "Galpón 1", 2, 1, 1, 0, 50) })
        };

        var g = Assert.Single(ReporteDiarioCostosEngordeCalculos.ConstruirTotales(filas).PorGalpon);

        Assert.Equal(2, g.Mortalidad);
        Assert.Equal(3, g.MortSel);
        Assert.Equal(0, g.MortSelHembras);
        Assert.Equal(0, g.MortSelMachos);
    }

    [Theory]
    [InlineData(false, true)]   // Ecuador (y toda empresa que maneja el engorde por sexo)
    [InlineData(true, false)]   // Panamá: la mortalidad mixta vive en la columna H ⇒ sin desglose
    public void MuestraMortalidadPorSexo_SoloSiElEngordeNoEsMixto(bool seguimientoEngordeMixto, bool esperado)
    {
        Assert.Equal(esperado, ReporteDiarioCostosEngordeCalculos.MuestraMortalidadPorSexo(seguimientoEngordeMixto));
    }

    [Fact]
    public void GalponDiaDto_DeserializaLasClavesPorSexoDeLaFn()
    {
        // Contrato fn ↔ DTO: el service parsea el JSON `galpones` con SnakeCaseLower. Un nombre mal
        // escrito no rompe nada: deja el campo en 0 en silencio. JSON real de la fn v4
        // (Kilometro 22, Galpon-2, 28-ago).
        const string json = """
            [{"mort_sel": 39, "galpon_id": "G0036", "seleccion": 25, "aves_vivas": 47299, "consumo_kg": 2400,
              "err_sexaje": 0, "mortalidad": 14, "galpon_nombre": "Galpon-2", "mort_sel_machos": 25,
              "mort_sel_hembras": 14, "seleccion_machos": 15, "mortalidad_machos": 10,
              "seleccion_hembras": 10, "mortalidad_hembras": 4}]
            """;
        var opts = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        var g = Assert.Single(System.Text.Json.JsonSerializer.Deserialize<List<ReporteDiarioCostosGalponDiaDto>>(json, opts)!);

        Assert.Equal(39, g.MortSel);
        Assert.Equal(4, g.MortalidadHembras);
        Assert.Equal(10, g.MortalidadMachos);
        Assert.Equal(10, g.SeleccionHembras);
        Assert.Equal(15, g.SeleccionMachos);
        Assert.Equal(14, g.MortSelHembras);
        Assert.Equal(25, g.MortSelMachos);
    }

    [Fact]
    public void ConstruirTotales_RedondeaKgATresDecimales()
    {
        var filas = new[]
        {
            Fila(new DateTime(2026, 7, 1), 0.1004,
                new[] { new ReporteDiarioCostosAlimentoDto("A", null, 0.1004) },
                new[] { Galpon("G1", "Galpón 1", 0, 0, 0, 0.1004, 1) }),
            Fila(new DateTime(2026, 7, 2), 0.2003,
                new[] { new ReporteDiarioCostosAlimentoDto("A", null, 0.2003) },
                new[] { Galpon("G1", "Galpón 1", 0, 0, 0, 0.2003, 1) })
        };

        var tot = ReporteDiarioCostosEngordeCalculos.ConstruirTotales(filas);

        Assert.Equal(0.301, tot.ConsumoTotalKg);            // 0.3007 → 0.301
        Assert.Equal(0.301, tot.Alimentos[0].ConsumoKg);
    }

    // ─────────────────────────────── AvesVivasActuales ───────────────────────────────

    [Fact]
    public void AvesVivasActuales_SinFilas_VacioYCero()
    {
        var (porGalpon, total) = ReporteDiarioCostosEngordeCalculos.AvesVivasActuales(Array.Empty<ReporteDiarioCostosFilaDto>());

        Assert.Empty(porGalpon);
        Assert.Equal(0, total);
    }

    [Fact]
    public void AvesVivasActuales_TomaLaUltimaFecha_AunSiLlegaDesordenada()
    {
        var filas = new[]
        {
            Fila(new DateTime(2026, 7, 3), 0,
                Array.Empty<ReporteDiarioCostosAlimentoDto>(),
                new[] { Galpon("G1", "Galpón 1", 0, 0, 0, 0, 4), Galpon("G2", "Galpón 2", 0, 0, 0, 0, 136) }),
            Fila(new DateTime(2026, 7, 1), 0,
                Array.Empty<ReporteDiarioCostosAlimentoDto>(),
                new[] { Galpon("G1", "Galpón 1", 0, 0, 0, 0, 10), Galpon("G2", "Galpón 2", 0, 0, 0, 0, 140) })
        };

        var (porGalpon, total) = ReporteDiarioCostosEngordeCalculos.AvesVivasActuales(filas);

        Assert.Equal(140, total);                       // 4 + 136 (fila del 3-jul)
        Assert.Equal(4, porGalpon.Single(g => g.GalponId == "G1").AvesVivas);
        Assert.Equal(136, porGalpon.Single(g => g.GalponId == "G2").AvesVivas);
    }

    // ───────────────────── FiltrarPorGalponesVisibles (alcance granular) ─────────────────────

    [Fact]
    public void FiltrarPorGalponesVisibles_RecortaGalpones_YRecalculaTotalesDelDia()
    {
        var filas = new[]
        {
            Fila(new DateTime(2026, 7, 1), 39,
                new[] { new ReporteDiarioCostosAlimentoDto("Alimento a", 2000, 34) },
                new[]
                {
                    Galpon("G1", "Galpón 1", 2, 1, 0, 20.1234, 4),
                    Galpon("G2", "Galpón 2", 3, 0, 1, 18.8766, 136)
                })
        };

        var visibles = ReporteDiarioCostosEngordeCalculos.FiltrarPorGalponesVisibles(
            filas, new HashSet<string> { "G2" });

        var fila = Assert.Single(visibles);
        Assert.Equal("G2", Assert.Single(fila.Galpones).GalponId);
        // Totales del día recalculados con la MISMA aritmética de la fn (suma por galpón + RedondearKg)
        Assert.Equal(ReporteDiarioCostosEngordeCalculos.RedondearKg(18.8766), fila.ConsumoTotalKg);
        Assert.Equal(3, fila.MortSelTotal);   // solo G2 (mort 3 + sel 0)
        Assert.Equal(136, fila.AvesVivasTotal);
        // El desglose de alimento es de GRANJA COMPLETA (no atribuible por galpón) → se descarta
        Assert.Empty(fila.Alimentos);
    }

    [Fact]
    public void FiltrarPorGalponesVisibles_FilaSinGalponVisible_SeDescarta()
    {
        var filas = new[]
        {
            Fila(new DateTime(2026, 7, 1), 10,
                Array.Empty<ReporteDiarioCostosAlimentoDto>(),
                new[] { Galpon("G1", "Galpón 1", 1, 0, 0, 10, 50) })
        };

        var visibles = ReporteDiarioCostosEngordeCalculos.FiltrarPorGalponesVisibles(
            filas, new HashSet<string> { "G9" });

        Assert.Empty(visibles);
    }

    [Fact]
    public void FiltrarPorGalponesVisibles_TodosVisibles_NoAlteraLaAritmetica()
    {
        var galpones = new[]
        {
            Galpon("G1", "Galpón 1", 2, 1, 0, 20.5, 4),
            Galpon("G2", "Galpón 2", 3, 0, 1, 19.5, 136)
        };
        var filas = new[]
        {
            Fila(new DateTime(2026, 7, 1), 40, Array.Empty<ReporteDiarioCostosAlimentoDto>(), galpones)
        };

        var visibles = ReporteDiarioCostosEngordeCalculos.FiltrarPorGalponesVisibles(
            filas, new HashSet<string> { "G1", "G2" });

        var fila = Assert.Single(visibles);
        Assert.Equal(2, fila.Galpones.Count);
        Assert.Equal(40, fila.ConsumoTotalKg);          // 20.5 + 19.5
        Assert.Equal(6, fila.MortSelTotal);             // (2+1) + (3+0)
        Assert.Equal(140, fila.AvesVivasTotal);
    }
}
