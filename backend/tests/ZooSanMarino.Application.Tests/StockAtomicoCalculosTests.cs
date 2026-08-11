using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de A1/A2 (`fase_de_desarrollo/f0a_stock_atomico_plan.md`).
///
/// El defecto original era que dos consumos concurrentes pasaban los dos la validación
/// `if (stock.Quantity &lt; req.Quantity) throw` y el saldo terminaba en negativo: se despachaba
/// alimento que no existía. La decisión se movió a la base; lo que se prueba acá es que el lado C#
/// interpreta bien el resultado y que la clave natural colapsa igual que el índice único.
/// </summary>
public class StockAtomicoCalculosTests
{
    // ─── Cantidad operable ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.001)]
    [InlineData(1)]
    [InlineData(12345.678)]
    public void Cantidad_positiva_es_operable(decimal cantidad)
    {
        Assert.True(StockAtomicoCalculos.EsCantidadOperable(cantidad));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.001)]
    public void Cantidad_cero_o_negativa_no_es_operable(decimal cantidad)
    {
        // El 0 importa tanto como el negativo: `quantity - 0` afectaría una fila y el
        // resultado se leería como "descuento aplicado" sin haber descontado nada.
        Assert.False(StockAtomicoCalculos.EsCantidadOperable(cantidad));
    }

    // ─── Interpretación del UPDATE condicional ─────────────────────────────────

    [Fact]
    public void Cero_filas_afectadas_es_RECHAZO_no_es_no_paso_nada()
    {
        // Este es el corazón de A2. `UPDATE ... WHERE quantity >= @q` no afecta filas cuando no
        // hay saldo. Interpretar ese 0 como "no pasó nada" y seguir adelante reintroduciría el
        // saldo negativo por otro camino.
        Assert.False(StockAtomicoCalculos.DescuentoAplicado(0));
    }

    [Fact]
    public void Una_fila_afectada_es_descuento_aplicado()
    {
        Assert.True(StockAtomicoCalculos.DescuentoAplicado(1));
    }

    [Fact]
    public void El_mensaje_de_stock_insuficiente_no_cambia()
    {
        // Congelado a propósito: el front lo muestra tal cual y hay flujos que lo comparan
        // para decidir si degradan el error o lo propagan.
        Assert.Equal("No hay stock suficiente para el consumo.", StockAtomicoCalculos.MensajeStockInsuficiente);
    }

    // ─── Clave natural: espejo del COALESCE del índice único ───────────────────

    [Fact]
    public void Null_y_cadena_vacia_de_ubicacion_son_la_MISMA_clave()
    {
        // Es el caso que hace falla el índice sin COALESCE: en Postgres NULL != NULL dentro de
        // un índice único, así que todo el stock a nivel granja (Colombia y las granjas con
        // maneja_alimento_por_galpon = false) se podría duplicar igual.
        var conNull = StockAtomicoCalculos.ClaveNatural(10, 55, null, null);
        var conVacio = StockAtomicoCalculos.ClaveNatural(10, 55, "", "");

        Assert.Equal(conNull, conVacio);
    }

    [Fact]
    public void Los_espacios_en_blanco_tambien_colapsan_a_nivel_granja()
    {
        var conNull = StockAtomicoCalculos.ClaveNatural(10, 55, null, null);
        var conEspacios = StockAtomicoCalculos.ClaveNatural(10, 55, "   ", "\t");

        Assert.Equal(conNull, conEspacios);
    }

    [Fact]
    public void La_ubicacion_se_recorta_para_que_no_haya_claves_gemelas_por_un_espacio()
    {
        var limpia = StockAtomicoCalculos.ClaveNatural(10, 55, "N1", "G1");
        var conEspacios = StockAtomicoCalculos.ClaveNatural(10, 55, " N1 ", " G1 ");

        Assert.Equal(limpia, conEspacios);
    }

    [Fact]
    public void Ubicaciones_distintas_son_claves_distintas()
    {
        var a = StockAtomicoCalculos.ClaveNatural(10, 55, "N1", "G1");
        var b = StockAtomicoCalculos.ClaveNatural(10, 55, "N1", "G2");
        var nivelGranja = StockAtomicoCalculos.ClaveNatural(10, 55, null, null);

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, nivelGranja);
    }

    [Fact]
    public void La_granja_y_el_item_forman_parte_de_la_clave()
    {
        var baseK = StockAtomicoCalculos.ClaveNatural(10, 55, "N1", "G1");

        Assert.NotEqual(baseK, StockAtomicoCalculos.ClaveNatural(11, 55, "N1", "G1"));
        Assert.NotEqual(baseK, StockAtomicoCalculos.ClaveNatural(10, 56, "N1", "G1"));
    }

    [Fact]
    public void Normalizar_devuelve_cadena_vacia_para_lo_que_es_ausencia_de_ubicacion()
    {
        Assert.Equal(string.Empty, StockAtomicoCalculos.NormalizarComponenteUbicacion(null));
        Assert.Equal(string.Empty, StockAtomicoCalculos.NormalizarComponenteUbicacion(""));
        Assert.Equal(string.Empty, StockAtomicoCalculos.NormalizarComponenteUbicacion("  "));
        Assert.Equal("N1", StockAtomicoCalculos.NormalizarComponenteUbicacion(" N1 "));
    }
}
