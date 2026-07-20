using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Verifica el consolidado de corrida Panamá: mismas fórmulas que fn_reporte_indicadores_panama
/// aplicadas sobre los insumos sumados de los lotes/galpones (identidad con 1 lote, sumas y
/// ponderación con varios, guards de división por cero y lista vacía).
/// </summary>
public class ReporteIndicadorPanamaCalculosTests
{
    private const decimal Tol = 0.0001m;

    /// <summary>Arma un reporte por lote como lo devolvería la fn (derivados con sus fórmulas).</summary>
    private static ReporteIndicadoresPanamaDto Reporte(
        decimal metros, decimal avesFinal, decimal prodKiloPie, int diasEngorde, int diasEnGranja,
        int avesBeneficiada, int avesEncasetadas, decimal consumoQq, decimal seleccion, decimal muertas)
    {
        decimal consumoKg = consumoQq * 45.36m;
        decimal pesoProm  = avesBeneficiada > 0 ? prodKiloPie / avesBeneficiada : 0m;
        decimal mortPorc  = avesEncasetadas > 0 ? muertas   / avesEncasetadas * 100m : 0m;
        decimal selPorc   = avesEncasetadas > 0 ? seleccion / avesEncasetadas * 100m : 0m;
        decimal conv      = prodKiloPie > 0 ? consumoKg / prodKiloPie : 0m;
        decimal consAve   = avesBeneficiada > 0 ? consumoKg / avesBeneficiada : 0m;
        decimal mortTotal = mortPorc + selPorc;
        decimal superv    = 100m - mortTotal;
        decimal ea        = diasEngorde > 0 ? pesoProm * 2.2046m / diasEngorde : 0m;
        decimal eef       = diasEngorde > 0 && conv > 0 ? prodKiloPie * superv / (diasEngorde * conv) * 100m : 0m;
        decimal eefDos    = diasEngorde > 0 && conv > 0 ? pesoProm * superv / (diasEngorde * conv) * 100m : 0m;
        decimal avesM2    = metros > 0 ? avesBeneficiada / metros : 0m;
        decimal kilosM2   = metros > 0 ? prodKiloPie / metros : 0m;
        decimal prodt     = diasEngorde > 0 && conv > 0 ? (pesoProm * 2.2046m / diasEngorde) / conv : 0m;

        return new ReporteIndicadoresPanamaDto(
            new LiquidacionPanamaDto(
                Id: 1, IdUsuarioRegistro: null, IdLote: 1,
                MetrosCuadrados: metros, AvesFinalGranja: avesFinal, ProduccionKiloPie: prodKiloPie,
                DiasEngorde: diasEngorde, DiasEnGranja: diasEnGranja, AvesBeneficiada: avesBeneficiada,
                PesoPromedio: pesoProm, MortalidadPorc: mortPorc, SeleccionPorc: selPorc,
                PorcMortalidadTotal: mortTotal, Supervivencia: superv, ConsumoAve: consAve,
                Conversion: conv, EficienciaAmericana: ea, EeF: eef, EefDos: eefDos,
                AvesMetrosCua: avesM2, KilosMetrosCua: kilosM2, Productividad: prodt,
                FaltanteSobra: avesFinal - avesBeneficiada),
            new InfoProductivaPanamaDto(consumoQq, seleccion, muertas),
            avesEncasetadas);
    }

    // ── Identidad: consolidar UN lote reproduce sus propios derivados ─────────
    [Fact]
    public void ConsolidarCorrida_UnLote_ReproduceSusDerivados()
    {
        var lote = Reporte(
            metros: 1200m, avesFinal: 11500m, prodKiloPie: 26450.75m,
            diasEngorde: 40, diasEnGranja: 43, avesBeneficiada: 11480,
            avesEncasetadas: 12000, consumoQq: 837.5m, seleccion: 120m, muertas: 380m);

        var c = ReporteIndicadorPanamaCalculos.ConsolidarCorrida(new[] { lote });

        Assert.NotNull(c);
        var l = c!.Liquidacion;
        var e = lote.Liquidacion;
        Assert.Equal(e.MetrosCuadrados, l.MetrosCuadrados);
        Assert.Equal(e.AvesFinalGranja, l.AvesFinalGranja);
        Assert.Equal(e.ProduccionKiloPie, l.ProduccionKiloPie);
        Assert.Equal(e.DiasEngorde, l.DiasEngorde);
        Assert.Equal(e.DiasEnGranja, l.DiasEnGranja);
        Assert.Equal(e.AvesBeneficiada, l.AvesBeneficiada);
        Assert.Equal(lote.AvesEncasetadas, c.AvesEncasetadas);
        Assert.InRange(l.PesoPromedio - e.PesoPromedio, -Tol, Tol);
        Assert.InRange(l.MortalidadPorc - e.MortalidadPorc, -Tol, Tol);
        Assert.InRange(l.SeleccionPorc - e.SeleccionPorc, -Tol, Tol);
        Assert.InRange(l.PorcMortalidadTotal - e.PorcMortalidadTotal, -Tol, Tol);
        Assert.InRange(l.Supervivencia - e.Supervivencia, -Tol, Tol);
        Assert.InRange(l.ConsumoAve - e.ConsumoAve, -Tol, Tol);
        Assert.InRange(l.Conversion - e.Conversion, -Tol, Tol);
        Assert.InRange(l.EficienciaAmericana - e.EficienciaAmericana, -Tol, Tol);
        Assert.InRange(l.EeF - e.EeF, -Tol, Tol);
        Assert.InRange(l.EefDos - e.EefDos, -Tol, Tol);
        Assert.InRange(l.AvesMetrosCua - e.AvesMetrosCua, -Tol, Tol);
        Assert.InRange(l.KilosMetrosCua - e.KilosMetrosCua, -Tol, Tol);
        Assert.InRange(l.Productividad - e.Productividad, -Tol, Tol);
        Assert.Equal(e.FaltanteSobra, l.FaltanteSobra);
        Assert.Equal(lote.InfoProductiva.ConsumoAlimentoTotal, c.InfoProductiva.ConsumoAlimentoTotal);
    }

    // ── Varios lotes: sumas crudas y derivados sobre los totales ──────────────
    [Fact]
    public void ConsolidarCorrida_DosLotes_SumaInsumosYRecalculaDerivados()
    {
        var a = Reporte(1000m, 10000m, 22000m, 40, 42, 9950, 10500, 700m, 100m, 350m);
        var b = Reporte(800m, 8000m, 17000m, 38, 40, 7900, 8300, 550m, 80m, 300m);

        var c = ReporteIndicadorPanamaCalculos.ConsolidarCorrida(new[] { a, b })!;
        var l = c.Liquidacion;

        Assert.Equal(1800m, l.MetrosCuadrados);
        Assert.Equal(18000m, l.AvesFinalGranja);
        Assert.Equal(39000m, l.ProduccionKiloPie);
        Assert.Equal(17850, l.AvesBeneficiada);
        Assert.Equal(18800, c.AvesEncasetadas);
        Assert.Equal(1250m, c.InfoProductiva.ConsumoAlimentoTotal); // qq
        Assert.Equal(180m, c.InfoProductiva.TotalAvesSeleccion);
        Assert.Equal(650m, c.InfoProductiva.TotalAvesMuertas);

        decimal consumoKg = 1250m * 45.36m;
        Assert.InRange(l.PesoPromedio - 39000m / 17850, -Tol, Tol);
        Assert.InRange(l.MortalidadPorc - 650m / 18800 * 100m, -Tol, Tol);
        Assert.InRange(l.SeleccionPorc - 180m / 18800 * 100m, -Tol, Tol);
        Assert.InRange(l.Conversion - consumoKg / 39000m, -Tol, Tol);
        Assert.InRange(l.ConsumoAve - consumoKg / 17850, -Tol, Tol);
        Assert.InRange(l.Supervivencia - (100m - (650m + 180m) / 18800 * 100m), -Tol, Tol);
        Assert.Equal(18000m - 17850, l.FaltanteSobra);
        Assert.InRange(l.AvesMetrosCua - 17850 / 1800m, -Tol, Tol);
        Assert.InRange(l.KilosMetrosCua - 39000m / 1800m, -Tol, Tol);
    }

    // ── Días consolidados: promedio ponderado por aves encasetadas ────────────
    [Fact]
    public void ConsolidarCorrida_DiasPonderadosPorAvesEncasetadas()
    {
        // 40 días con 10500 aves y 38 días con 8300 → (40·10500 + 38·8300)/18800 = 39.117 → 39
        var a = Reporte(1000m, 10000m, 22000m, 40, 42, 9950, 10500, 700m, 100m, 350m);
        var b = Reporte(800m, 8000m, 17000m, 38, 40, 7900, 8300, 550m, 80m, 300m);

        var c = ReporteIndicadorPanamaCalculos.ConsolidarCorrida(new[] { a, b })!;

        Assert.Equal(39, c.Liquidacion.DiasEngorde);
        Assert.Equal(41, c.Liquidacion.DiasEnGranja); // (42·10500 + 40·8300)/18800 = 41.117 → 41
    }

    [Fact]
    public void ConsolidarCorrida_SinAvesEncasetadas_PromedioSimpleDeDias()
    {
        var a = Reporte(0m, 0m, 0m, 40, 44, 0, 0, 0m, 0m, 0m);
        var b = Reporte(0m, 0m, 0m, 37, 41, 0, 0, 0m, 0m, 0m);

        var c = ReporteIndicadorPanamaCalculos.ConsolidarCorrida(new[] { a, b })!;

        Assert.Equal(39, c.Liquidacion.DiasEngorde);  // (40+37)/2 = 38.5 → 39 (AwayFromZero)
        Assert.Equal(43, c.Liquidacion.DiasEnGranja); // (44+41)/2 = 42.5 → 43
    }

    // ── Guards de división por cero: mismos ceros que la fn ───────────────────
    [Fact]
    public void ConsolidarCorrida_InsumosEnCero_DerivadosEnCeroComoLaFn()
    {
        var vacio = Reporte(0m, 0m, 0m, 0, 0, 0, 0, 0m, 0m, 0m);

        var c = ReporteIndicadorPanamaCalculos.ConsolidarCorrida(new[] { vacio })!;
        var l = c.Liquidacion;

        Assert.Equal(0m, l.PesoPromedio);
        Assert.Equal(0m, l.MortalidadPorc);
        Assert.Equal(0m, l.SeleccionPorc);
        Assert.Equal(0m, l.Conversion);
        Assert.Equal(0m, l.ConsumoAve);
        Assert.Equal(0m, l.EficienciaAmericana);
        Assert.Equal(0m, l.EeF);
        Assert.Equal(0m, l.EefDos);
        Assert.Equal(0m, l.AvesMetrosCua);
        Assert.Equal(0m, l.KilosMetrosCua);
        Assert.Equal(0m, l.Productividad);
        Assert.Equal(100m, l.Supervivencia); // 100 − 0, igual que la fn
    }

    // ── Lista vacía / null → null (la corrida no tiene liquidaciones) ─────────
    [Fact]
    public void ConsolidarCorrida_SinReportes_DevuelveNull()
    {
        Assert.Null(ReporteIndicadorPanamaCalculos.ConsolidarCorrida(Array.Empty<ReporteIndicadoresPanamaDto>()));
        Assert.Null(ReporteIndicadorPanamaCalculos.ConsolidarCorrida(null!));
    }
}
