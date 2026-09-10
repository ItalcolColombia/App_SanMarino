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

    // ── ValidarPesoObligatorioEnVenta ────────────────────────────────────────────────────────
    // El caso que motivó el gate: Panamá digitaba el MISMO número en bruto y tara (planta entrega
    // UNA sola cifra de kilos y el formulario pide dos) ⇒ neto 0, la venta contaba las aves y
    // aportaba 0 kg al seguimiento diario, al informe semanal y a la liquidación.

    [Fact]
    public void ValidarPeso_BrutoIgualTara_Rechaza()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 5060d, 5060d));

        Assert.Contains("no puede ser 0 kg", ex.Message);
    }

    [Fact]
    public void ValidarPeso_BrutoIgualTara_TambienConPesoDiferido()
    {
        // El flag sólo tolera la AUSENCIA total de peso; un neto 0 declarado sigue siendo error.
        Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 5060d, 5060d, true));
    }

    [Fact]
    public void ValidarPeso_SoloKilosNetos_TaraEnCero_Pasa()
    {
        // La salida que se le indica al operario en el mensaje del gate.
        MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 5060d, 0d);
    }

    [Fact]
    public void ValidarPeso_BrutoMayorQueTara_Pasa()
    {
        MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 10170d, 7530d);
    }

    // ── Equivalencia: los mensajes previos quedan byte a byte iguales ────────────────────────

    [Fact]
    public void ValidarPeso_BrutoMenorQueTara_ConservaSuMensaje()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 100d, 200d));

        Assert.Equal("El peso bruto no puede ser menor que el peso tara.", ex.Message);
    }

    [Fact]
    public void ValidarPeso_SinPeso_ConservaSuMensaje()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", null, null));

        Assert.Equal(
            "El peso báscula es obligatorio para registrar la venta: indique peso bruto y peso tara.",
            ex.Message);
    }

    [Fact]
    public void ValidarPeso_SinPeso_ConDiferido_Pasa()
    {
        MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", null, null, true);
    }

    [Fact]
    public void ValidarPeso_PesoAMedias_ConDiferido_SigueSiendoError()
    {
        Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 5060d, null, true));
    }

    [Fact]
    public void ValidarPeso_NoEsVenta_NoValida()
    {
        // Los traslados no pasan por báscula: bruto == tara (o ausente) es legal.
        MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Traslado", 5060d, 5060d);
        MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Traslado", null, null);
    }
}
