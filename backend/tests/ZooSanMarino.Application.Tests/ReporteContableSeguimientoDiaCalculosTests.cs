using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// El reporte contable consolida los registros del mismo lote y día antes de armar su fila diaria.
/// Con un registro por día la salida es ese mismo registro (sin cambios para empresas sin duplicados).
/// </summary>
public class ReporteContableSeguimientoDiaCalculosTests
{
    private static DateTime D(int dia, int hora = 12) => new(2026, 9, dia, hora, 0, 0, DateTimeKind.Utc);

    private static SeguimientoLevanteContableFila Lev(int lote, DateTime fecha, int? mh, int? sh = 0, decimal? ch = 0m) =>
        new(lote, fecha, mh, 0, sh, 0, ch, 0m);

    [Fact]
    public void Levante_UnRegistroPorDia_DevuelveElMismoRegistro()
    {
        var a = Lev(155, D(3), 5, 1, 100m);
        var b = Lev(155, D(4), 15, 0, 3000m);

        var res = ReporteContableSeguimientoDiaCalculos.AgruparLevantePorLoteDia([a, b]);

        Assert.Equal(2, res.Count);
        Assert.Same(a, res[0]);
        Assert.Same(b, res[1]);
    }

    /// <summary>Caso medido en la validación de Santa Reyes: 15/0/3.000 + 3/1/100.</summary>
    [Fact]
    public void Levante_DosRegistrosElMismoDia_SeSuman()
    {
        var res = ReporteContableSeguimientoDiaCalculos.AgruparLevantePorLoteDia(
        [
            Lev(155, D(4, 17), 15, 0, 3000m),
            Lev(155, D(4, 17), 3, 1, 100m)
        ]);

        var dia = Assert.Single(res);
        Assert.Equal(18, dia.MortalidadHembras);
        Assert.Equal(1, dia.SelH);
        Assert.Equal(3100m, dia.ConsumoKgHembras);
        Assert.Equal(D(4, 17), dia.Fecha);
    }

    /// <summary>
    /// Todos null ⇒ null: el reporte hace <c>levante?.MortalidadHembras ?? produccion?.MortalidadH</c>
    /// y un 0 inventado taparía la producción del día de transición.
    /// </summary>
    [Fact]
    public void Levante_TodosNull_QuedaNull()
    {
        var res = ReporteContableSeguimientoDiaCalculos.AgruparLevantePorLoteDia(
            [Lev(1, D(4), null, null, null), Lev(1, D(4, 18), null, null, null)]);

        var dia = Assert.Single(res);
        Assert.Null(dia.MortalidadHembras);
        Assert.Null(dia.SelH);
        Assert.Null(dia.ConsumoKgHembras);
    }

    /// <summary>Par Demo: manual (50/30) + fila de traslado (0) — antes podía salir 0.</summary>
    [Fact]
    public void Levante_ManualMasTraslado_ConservaLaMortalidadDelManual()
    {
        var res = ReporteContableSeguimientoDiaCalculos.AgruparLevantePorLoteDia(
            [Lev(120, D(30, 0), 0, 0, null), Lev(120, D(30, 12), 50, 30, 0m)]);

        var dia = Assert.Single(res);
        Assert.Equal(50, dia.MortalidadHembras);
        Assert.Equal(30, dia.SelH);
        Assert.Equal(0m, dia.ConsumoKgHembras);
    }

    [Fact]
    public void Levante_NoMezclaLotesNiDias()
    {
        var res = ReporteContableSeguimientoDiaCalculos.AgruparLevantePorLoteDia(
            [Lev(1, D(4), 1), Lev(2, D(4), 2), Lev(1, D(5), 3), Lev(1, D(4, 18), 4)]);

        Assert.Equal(3, res.Count);
        Assert.Equal(5, res.Single(r => r.LoteId == 1 && r.Fecha.Date == D(4).Date).MortalidadHembras);
        Assert.Equal(2, res.Single(r => r.LoteId == 2).MortalidadHembras);
        Assert.Equal(3, res.Single(r => r.Fecha.Date == D(5).Date).MortalidadHembras);
    }

    /// <summary>Caso medido en la validación de Santa Reyes: 5/399 + 2/50.</summary>
    [Fact]
    public void Produccion_DosRegistrosElMismoDia_SeSuman()
    {
        var res = ReporteContableSeguimientoDiaCalculos.AgruparProduccionPorLoteDia(
        [
            new SeguimientoProduccionContableFila(152, D(4), 5, 0, 0, 399m, 0m),
            new SeguimientoProduccionContableFila(152, D(4), 2, 0, 0, 50m, 0m),
            new SeguimientoProduccionContableFila(152, D(2), 125, 0, 0, 200m, 0m)
        ]);

        Assert.Equal(2, res.Count);
        var dia4 = res.Single(r => r.Fecha.Date == D(4).Date);
        Assert.Equal(7, dia4.MortalidadH);
        Assert.Equal(449m, dia4.ConsKgH);
        Assert.Equal(125, res.Single(r => r.Fecha.Date == D(2).Date).MortalidadH);
    }

    [Fact]
    public void Produccion_UnRegistroPorDia_DevuelveElMismoRegistro()
    {
        var a = new SeguimientoProduccionContableFila(7, D(1), 1, 0, 0, 10m, 0m);
        Assert.Same(a, ReporteContableSeguimientoDiaCalculos.AgruparProduccionPorLoteDia([a]).Single());
    }

    [Theory]
    [InlineData(new int[0], null)]
    [InlineData(new[] { 3 }, 3)]
    [InlineData(new[] { 3, 4 }, 7)]
    public void SumaOpcional_Enteros(int[] presentes, int? esperado)
    {
        var valores = presentes.Select(v => (int?)v).Append(null);
        Assert.Equal(esperado, ReporteContableSeguimientoDiaCalculos.SumaOpcional(valores));
    }
}
