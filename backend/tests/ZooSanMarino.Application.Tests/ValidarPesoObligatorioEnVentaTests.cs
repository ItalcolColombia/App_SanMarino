using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Peso báscula obligatorio al registrar ventas (regla tras el incidente de una
/// venta con pesos NULL que descuadró la liquidación: quedaba en 0 kg).
/// </summary>
public class ValidarPesoObligatorioEnVentaTests
{
    [Fact]
    public void Venta_SinPesos_Lanza()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", null, null));
        Assert.Contains("obligatorio", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, 100d)]   // falta bruto
    [InlineData(5000d, null)]  // falta tara
    public void Venta_PesoIncompleto_Lanza(double? bruto, double? tara)
    {
        Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", bruto, tara));
    }

    [Fact]
    public void Venta_BrutoCero_Lanza()
    {
        Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 0d, 0d));
    }

    [Fact]
    public void Venta_TaraNegativa_Lanza()
    {
        Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 100d, -1d));
    }

    [Fact]
    public void Venta_BrutoMenorQueTara_Lanza()
    {
        Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 100d, 200d));
    }

    [Fact]
    public void Venta_PesosValidos_NoLanza()
    {
        MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 5000d, 300d);
        MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 5000d, 0d);
    }

    [Theory]
    [InlineData("Traslado")]
    [InlineData("Retiro")]
    [InlineData(null)]
    public void NoVenta_SinPesos_NoLanza(string? tipo)
    {
        // Los movimientos que no son venta no pasan por báscula: sin cambio de comportamiento.
        MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta(tipo, null, null);
    }
}
