using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Equivalencia con la función SQL <c>fn_tipo_evento_inventario</c>, de la que depende decidir qué
/// movimientos de inventario obligan a refrescar el saldo de alimento de pollo engorde
/// (jul-2026: antes el saldo persistido solo se recalculaba al crear o editar un seguimiento, así
/// que un ingreso posterior al último día cargado quedaba invisible para la liquidación).
/// </summary>
public class TipoEventoInventarioCalculosTests
{
    // ─── Mapeo, espejo del ILIKE de la función SQL ────────────────────────────

    [Theory]
    [InlineData("Ingreso",                      "INV_INGRESO")]
    [InlineData("TrasladoEntrada",              "INV_TRASLADO_ENTRADA")]
    [InlineData("TrasladoInterGranjaEntrada",   "INV_TRASLADO_ENTRADA")]
    [InlineData("TrasladoSalida",               "INV_TRASLADO_SALIDA")]
    [InlineData("TrasladoInterGranjaSalida",    "INV_TRASLADO_SALIDA")]
    [InlineData("TrasladoInterGranjaPendiente", "INV_TRASLADO_SALIDA")]
    [InlineData("Consumo",                      "INV_CONSUMO")]
    [InlineData("AjusteCuadreTablaEntrada",     "INV_AJUSTE_CUADRE_ENTRADA")]
    [InlineData("AjusteCuadreTablaSalida",      "INV_AJUSTE_CUADRE_SALIDA")]
    [InlineData("AjusteStock",                  "INV_OTRO")]
    [InlineData("EliminacionStock",             "INV_OTRO")]
    [InlineData("TrasladoInterGranjaRechazado", "INV_OTRO")]
    public void TipoEvento_MapeaComoLaFuncionSql(string movementType, string esperado)
        => Assert.Equal(esperado, TipoEventoInventarioCalculos.TipoEvento(movementType));

    [Theory]
    [InlineData("ajustecuadretablasalida",  "INV_AJUSTE_CUADRE_SALIDA")]
    [InlineData("  AjusteCuadreTablaEntrada  ", "INV_AJUSTE_CUADRE_ENTRADA")]
    public void TipoEvento_AjusteDeCuadre_NoDistingueMayusculasNiEspacios(string movementType, string esperado)
        => Assert.Equal(esperado, TipoEventoInventarioCalculos.TipoEvento(movementType));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("algo que nadie mapeo todavia")]
    public void TipoEvento_DesconocidoOVacio_CaeEnInvOtro(string? movementType)
        => Assert.Equal("INV_OTRO", TipoEventoInventarioCalculos.TipoEvento(movementType));

    [Theory]
    [InlineData("ingreso")]
    [InlineData("INGRESO")]
    [InlineData("  Ingreso  ")]
    public void TipoEvento_NoDistingueMayusculasNiEspacios(string movementType)
        => Assert.Equal("INV_INGRESO", TipoEventoInventarioCalculos.TipoEvento(movementType));

    // ─── La decisión que gobierna el refresco del saldo ───────────────────────

    [Theory]
    [InlineData("Ingreso")]
    [InlineData("TrasladoEntrada")]
    [InlineData("TrasladoInterGranjaEntrada")]
    [InlineData("TrasladoSalida")]
    [InlineData("TrasladoInterGranjaSalida")]
    [InlineData("TrasladoInterGranjaPendiente")]
    public void EntradasYSalidas_AfectanElSaldo(string movementType)
        => Assert.True(TipoEventoInventarioCalculos.AfectaSaldoAlimentoEngorde(movementType));

    [Theory]
    [InlineData("AjusteCuadreTablaEntrada")]
    [InlineData("AjusteCuadreTablaSalida")]
    public void AjustesDeCuadre_AfectanElSaldo(string movementType)
    {
        // Existen para mover la tabla diaria y la fn los lee desde la v17. Si no afectaran el saldo,
        // el ajuste se escribiría y la columna persistida quedaría vieja: exactamente el hueco por
        // el que «Eliminar registro de stock» dejó la tabla de CAROLINA G1 alta (TK-2026-000183).
        Assert.True(TipoEventoInventarioCalculos.AfectaSaldoAlimentoEngorde(movementType));
    }

    [Fact]
    public void Consumo_NO_AfectaElSaldo()
    {
        // El saldo resta el consumo del SEGUIMIENTO, no el del inventario: contarlo acá lo
        // descontaría dos veces.
        Assert.False(TipoEventoInventarioCalculos.AfectaSaldoAlimentoEngorde("Consumo"));
    }

    [Theory]
    [InlineData("AjusteStock")]
    [InlineData("EliminacionStock")]
    public void AjustesManuales_NO_AfectanElSaldo(string movementType)
    {
        // Entran al histórico como INV_OTRO y ningún cálculo del saldo mira ese tipo; además el
        // ajuste guarda la cantidad en valor absoluto, sin el signo del delta.
        Assert.False(TipoEventoInventarioCalculos.AfectaSaldoAlimentoEngorde(movementType));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tipo nuevo sin mapear")]
    public void TipoDesconocido_NO_AfectaElSaldo_FailClosed(string? movementType)
        => Assert.False(TipoEventoInventarioCalculos.AfectaSaldoAlimentoEngorde(movementType));
}
