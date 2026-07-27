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

    // ── Peso diferido (flag de empresa venta_engorde_peso_diferido — Panamá) ────────────────
    // La báscula llega al día siguiente: la venta puede nacer SIN peso y queda "Pendiente"
    // hasta que se carga en la confirmación.

    /// <summary>Gate de no-regresión: con el flag apagado el mensaje debe ser IDÉNTICO al histórico.</summary>
    [Fact]
    public void PesoDiferido_Apagado_ComportamientoActualIntacto()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", null, null, pesoDiferidoPermitido: false));
        Assert.Equal(
            "El peso báscula es obligatorio para registrar la venta: indique peso bruto y peso tara.",
            ex.Message);
    }

    /// <summary>El default del parámetro es false ⇒ los 3 call-sites sin tocar no cambian.</summary>
    [Fact]
    public void PesoDiferido_DefaultEsApagado()
    {
        Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", null, null));
    }

    [Fact]
    public void PesoDiferido_Encendido_SinNingunPeso_NoLanza()
    {
        MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", null, null, pesoDiferidoPermitido: true);
    }

    /// <summary>Peso a medias = error de digitación, no báscula pendiente: sigue lanzando con el flag ON.</summary>
    [Theory]
    [InlineData(null, 100d)]
    [InlineData(5000d, null)]
    public void PesoDiferido_Encendido_PesoIncompleto_Lanza(double? bruto, double? tara)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", bruto, tara, pesoDiferidoPermitido: true));
        Assert.Equal(
            "El peso báscula es obligatorio para registrar la venta: indique peso bruto y peso tara.",
            ex.Message);
    }

    /// <summary>Con el flag ON, si el peso VIENE, se valida con las mismas reglas y mensajes de siempre.</summary>
    [Theory]
    [InlineData(0d, 0d, "El peso bruto de la venta debe ser mayor a 0 kg.")]
    [InlineData(100d, -1d, "El peso tara no puede ser negativo.")]
    [InlineData(100d, 200d, "El peso bruto no puede ser menor que el peso tara.")]
    public void PesoDiferido_Encendido_PesoInvalido_MismosMensajes(double bruto, double tara, string esperado)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", bruto, tara, pesoDiferidoPermitido: true));
        Assert.Equal(esperado, ex.Message);
    }

    [Fact]
    public void PesoDiferido_Encendido_PesosValidos_NoLanza()
    {
        MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 5000d, 300d, pesoDiferidoPermitido: true);
    }

    /// <summary>
    /// El registro de peso en la confirmación reusa esta misma validación con el flag APAGADO:
    /// al cargar la báscula el peso ya no es opcional.
    /// </summary>
    [Fact]
    public void RegistroDePesoEnConfirmacion_ExigeAmbosPesos()
    {
        Assert.Throws<InvalidOperationException>(() =>
            MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 5000d, null));
        MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta("Venta", 5000d, 300d);
    }
}
