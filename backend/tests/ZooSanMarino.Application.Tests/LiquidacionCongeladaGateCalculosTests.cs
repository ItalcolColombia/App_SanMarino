// tests/ZooSanMarino.Application.Tests/LiquidacionCongeladaGateCalculosTests.cs
//
// Gate de escritura sobre lotes de pollo engorde LIQUIDADOS (copia congelada).
// Contrato bajo prueba:
//   1. Con estado distinto de "Cerrado" (Abierto, vacío, null, otros) NINGUNA operación se bloquea
//      → comportamiento idéntico al previo a la feature.
//   2. Con "Cerrado" (cualquier casing) TODAS las operaciones de la lista cerrada se bloquean con
//      el mensaje canónico.
//   3. El bypass explícito (omitirGateLiquidado) deja pasar aun con el lote liquidado — es el
//      camino de la corrección de aves disponibles, que re-congela al terminar.
//   4. El mensaje identifica el lote cuando se le pasa el nombre (operaciones multi-lote).
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

public class LiquidacionCongeladaGateCalculosTests
{
    private static readonly OperacionLoteEngordeLiquidado[] TodasLasOperaciones =
        Enum.GetValues<OperacionLoteEngordeLiquidado>();

    // ── EstaLiquidado ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Cerrado")]
    [InlineData("cerrado")]
    [InlineData("CERRADO")]
    [InlineData("cErRaDo")]
    public void EstaLiquidado_true_para_cerrado_en_cualquier_casing(string estado)
        => Assert.True(LiquidacionCongeladaGateCalculos.EstaLiquidado(estado));

    [Theory]
    [InlineData("Abierto")]
    [InlineData("abierto")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData(" Cerrado")]   // con espacios NO es el literal que escriben los services
    [InlineData("Cerrado ")]
    [InlineData("EnTraslado")]
    public void EstaLiquidado_false_para_todo_lo_demas(string? estado)
        => Assert.False(LiquidacionCongeladaGateCalculos.EstaLiquidado(estado));

    // ── ValidarEscritura: lote NO liquidado ⇒ pasa siempre (comportamiento previo intacto) ──

    public static TheoryData<string?> EstadosNoLiquidados() => new() { "Abierto", "abierto", "", null };

    [Theory]
    [MemberData(nameof(EstadosNoLiquidados))]
    public void ValidarEscritura_no_bloquea_ninguna_operacion_con_lote_no_liquidado(string? estado)
    {
        foreach (var op in TodasLasOperaciones)
        {
            // No lanza — la operación sigue el flujo previo a la feature.
            LiquidacionCongeladaGateCalculos.ValidarEscritura(estado, op);
        }
    }

    // ── ValidarEscritura: lote liquidado ⇒ bloquea las 10 operaciones con el mensaje canónico ──

    [Theory]
    [InlineData(OperacionLoteEngordeLiquidado.EditarLote)]
    [InlineData(OperacionLoteEngordeLiquidado.EliminarLote)]
    [InlineData(OperacionLoteEngordeLiquidado.EliminarDefinitivoLote)]
    [InlineData(OperacionLoteEngordeLiquidado.AplicarCuadrarSaldos)]
    [InlineData(OperacionLoteEngordeLiquidado.BackfillMetadata)]
    [InlineData(OperacionLoteEngordeLiquidado.SeguimientoReproductora)]
    [InlineData(OperacionLoteEngordeLiquidado.ReproductoraLote)]
    [InlineData(OperacionLoteEngordeLiquidado.MovimientoAves)]
    [InlineData(OperacionLoteEngordeLiquidado.LiquidacionInsumosPanama)]
    [InlineData(OperacionLoteEngordeLiquidado.PuenteSincronizacion)]
    public void ValidarEscritura_bloquea_cada_operacion_con_lote_liquidado(OperacionLoteEngordeLiquidado op)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LiquidacionCongeladaGateCalculos.ValidarEscritura("Cerrado", op));
        Assert.Equal("El lote está liquidado. Reabra el lote para modificarlo.", ex.Message);
    }

    [Fact]
    public void ValidarEscritura_bloquea_tambien_con_casing_distinto()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LiquidacionCongeladaGateCalculos.ValidarEscritura(
                "cerrado", OperacionLoteEngordeLiquidado.MovimientoAves));
        Assert.Equal(LiquidacionCongeladaGateCalculos.MensajeBloqueo, ex.Message);
    }

    // ── Bypass explícito (corrección de aves disponibles) ────────────────────

    [Fact]
    public void ValidarEscritura_con_bypass_pasa_aunque_este_liquidado()
    {
        foreach (var op in TodasLasOperaciones)
        {
            LiquidacionCongeladaGateCalculos.ValidarEscritura(
                "Cerrado", op, omitirGateLiquidado: true);
        }
    }

    [Fact]
    public void ValidarEscritura_con_bypass_y_lote_abierto_tambien_pasa()
        => LiquidacionCongeladaGateCalculos.ValidarEscritura(
            "Abierto", OperacionLoteEngordeLiquidado.MovimientoAves, omitirGateLiquidado: true);

    // ── Mensaje con lote identificado (multi-lote) ───────────────────────────

    [Fact]
    public void ValidarEscritura_con_nombre_identifica_el_lote_en_el_mensaje()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LiquidacionCongeladaGateCalculos.ValidarEscritura(
                "Cerrado", OperacionLoteEngordeLiquidado.MovimientoAves, loteNombre: "2602"));
        Assert.Equal("El lote '2602' está liquidado. Reabra el lote para modificarlo.", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MensajeBloqueoCon_sin_nombre_usa_el_canonico(string? nombre)
        => Assert.Equal(
            LiquidacionCongeladaGateCalculos.MensajeBloqueo,
            LiquidacionCongeladaGateCalculos.MensajeBloqueoCon(nombre));

    [Fact]
    public void MensajeBloqueoCon_recorta_el_nombre()
        => Assert.Equal(
            "El lote '2602' está liquidado. Reabra el lote para modificarlo.",
            LiquidacionCongeladaGateCalculos.MensajeBloqueoCon("  2602  "));
}
