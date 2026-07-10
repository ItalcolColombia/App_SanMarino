using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Prorrateo de peso por lote en despachos multi-lote (G4/G5): la suma de los individuales debe
/// reconstruir EXACTAMENTE el global del camión (redondeo a 3 decimales con residuo al lote con
/// más aves), que es lo que consume la liquidación técnica Ecuador.
/// </summary>
public class MovimientoPolloEngordeCalculosTests
{
    [Fact]
    public void Prorrateo_TresLotes_SumaIndividualesIgualAlGlobal()
    {
        var aves = new[] { 5000, 3000, 2000 };
        var r = MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea(12000d, 2000d, aves);

        Assert.Equal(3, r.Length);
        Assert.Equal(12000d, r.Sum(x => x.Bruto ?? 0d), 3);
        Assert.Equal(2000d, r.Sum(x => x.Tara ?? 0d), 3);
        Assert.Equal(10000d, r.Sum(x => x.Neto ?? 0d), 3);
    }

    [Fact]
    public void Prorrateo_RepartoProporcionalALasAves()
    {
        var aves = new[] { 5000, 3000, 2000 };
        var r = MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea(12000d, 2000d, aves);

        // 50% / 30% / 20% del neto (10 000) — el residuo cae en la línea 0 (más aves).
        Assert.Equal(5000d, r[0].Neto!.Value, 3);
        Assert.Equal(3000d, r[1].Neto!.Value, 3);
        Assert.Equal(2000d, r[2].Neto!.Value, 3);
    }

    [Fact]
    public void Prorrateo_ResiduoDeRedondeoCaeEnLoteConMasAves()
    {
        // 3 líneas que no dividen exacto: 1/3 de 100 = 33.333…
        var aves = new[] { 7, 3, 3 };
        var r = MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea(100d, 0d, aves);

        Assert.Equal(100d, r.Sum(x => x.Neto ?? 0d), 3);
        // Las líneas chicas conservan el redondeo plano; el ajuste quedó en la línea 0.
        Assert.Equal(Math.Round(100d * 3 / 13, 3), r[1].Neto!.Value, 3);
        Assert.Equal(Math.Round(100d * 3 / 13, 3), r[2].Neto!.Value, 3);
        Assert.Equal(100d - r[1].Neto!.Value - r[2].Neto!.Value, r[0].Neto!.Value, 3);
    }

    [Fact]
    public void Prorrateo_TresDecimales()
    {
        var aves = new[] { 333, 667 };
        var r = MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea(1234.567d, 234.567d, aves);

        foreach (var linea in r)
        {
            Assert.Equal(Math.Round(linea.Bruto!.Value, 3), linea.Bruto!.Value, 10);
            Assert.Equal(Math.Round(linea.Tara!.Value, 3), linea.Tara!.Value, 10);
            Assert.Equal(Math.Round(linea.Neto!.Value, 3), linea.Neto!.Value, 10);
        }
        Assert.Equal(1000d, r.Sum(x => x.Neto ?? 0d), 3);
    }

    [Fact]
    public void Prorrateo_SinAves_DevuelveNulls()
    {
        var r = MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea(100d, 10d, new[] { 0, 0 });

        Assert.All(r, x =>
        {
            Assert.Null(x.Bruto);
            Assert.Null(x.Tara);
            Assert.Null(x.Neto);
            Assert.Null(x.Promedio);
        });
    }

    [Fact]
    public void Prorrateo_UnaSolaLinea_RecibeTodoElPeso()
    {
        var r = MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea(8000d, 1500d, new[] { 4200 });

        Assert.Single(r);
        Assert.Equal(8000d, r[0].Bruto!.Value, 3);
        Assert.Equal(1500d, r[0].Tara!.Value, 3);
        Assert.Equal(6500d, r[0].Neto!.Value, 3);
        Assert.Equal(6500d / 4200, r[0].Promedio!.Value, 6);
    }
}
