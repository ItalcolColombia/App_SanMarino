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
}
