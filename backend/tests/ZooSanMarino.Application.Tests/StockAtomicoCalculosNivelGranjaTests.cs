using ZooSanMarino.Application.Calculos;
using Xunit;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// F4 — el mensaje de stock insuficiente a nivel granja tiene que quedar BYTE A BYTE igual al que
/// tiraba <c>RegistrarConsumoNivelGranjaAsync</c> antes del cambio a descuento atómico. El front y el
/// reporte de la carga masiva lo comparan/muestran tal cual.
/// </summary>
public class StockAtomicoCalculosNivelGranjaTests
{
    [Fact]
    public void Literal_byte_a_byte_igual_al_de_antes_de_F4()
    {
        // Reproduce EXACTAMENTE el interpolado que tenía el service:
        // $"Stock insuficiente para '{item.Codigo} - {item.Nombre}' (granja {req.FarmId}): disponible
        //   {(stock?.Quantity ?? 0m):0.###}, requerido {req.Quantity:0.###}."
        var esperado = $"Stock insuficiente para 'AL01 - Alimento Iniciación' (granja 40): " +
                        $"disponible {12.5m:0.###}, requerido {20m:0.###}.";

        var real = StockAtomicoCalculos.MensajeStockInsuficienteNivelGranja(
            "AL01", "Alimento Iniciación", 40, 12.5m, 20m);

        Assert.Equal(esperado, real);
    }

    [Fact]
    public void Sin_stock_previo_disponible_es_cero_no_vacio()
    {
        // El original usaba `stock?.Quantity ?? 0m` para el caso "no había fila de stock".
        var r = StockAtomicoCalculos.MensajeStockInsuficienteNivelGranja("X", "Y", 1, 0m, 5m);
        Assert.Contains("disponible 0, requerido 5.", r);
    }

    [Theory]
    [InlineData(1.5, "1.5")]
    [InlineData(1.0, "1")]
    [InlineData(1.2345, "1.235")]
    [InlineData(1.2344, "1.234")]
    public void El_formato_de_cantidad_es_0punto3numeral(double valor, string esperado)
    {
        // "0.###" — hasta 3 decimales, sin ceros de relleno. Es EXACTAMENTE lo que usaba el original.
        var r = StockAtomicoCalculos.MensajeStockInsuficienteNivelGranja("X", "Y", 1, (decimal)valor, 0m);
        Assert.Contains($"disponible {esperado},", r);
    }

    [Fact]
    public void Nombra_el_item_la_granja_lo_disponible_y_lo_requerido()
    {
        var r = StockAtomicoCalculos.MensajeStockInsuficienteNivelGranja("COD9", "Un ítem", 777, 3m, 8m);

        Assert.Contains("COD9", r);
        Assert.Contains("Un ítem", r);
        Assert.Contains("777", r);
        Assert.Contains("3", r);
        Assert.Contains("8", r);
    }
}
