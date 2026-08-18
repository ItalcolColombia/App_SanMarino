using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de «no hay seguimiento diario sin alimento».
///
/// <para>
/// La regla no es «que haya kilos» sino «que haya kilos en el bloque que corresponde»: en pollo
/// engorde sobre el campo Mixto, en levante y producción sobre hembras y/o machos. Un registro con
/// el alimento cargado en el lugar equivocado es justamente el error que se quiere frenar, así que
/// tiene que fallar aunque el total sea distinto de cero.
/// </para>
/// </summary>
public class AlimentoObligatorioCalculosTests
{
    private static AlimentoCapturado Cap(decimal h = 0, decimal m = 0, decimal g = 0) => new(h, m, g);

    // ─── Cumple ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ModuloSeguimiento.Levante)]
    [InlineData(ModuloSeguimiento.Produccion)]
    public void Postura_ConAlimentoEnHembras_Cumple(string modulo)
    {
        Assert.True(AlimentoObligatorioCalculos.Cumple(modulo, loteEsMixto: false, Cap(h: 120.5m)));
    }

    [Theory]
    [InlineData(ModuloSeguimiento.Levante)]
    [InlineData(ModuloSeguimiento.Produccion)]
    public void Postura_ConAlimentoSoloEnMachos_Cumple(string modulo)
    {
        // «Debe ser alguno de los géneros, macho o hembra, o los dos»: machos solo alcanza.
        Assert.True(AlimentoObligatorioCalculos.Cumple(modulo, loteEsMixto: false, Cap(m: 30m)));
    }

    [Fact]
    public void Postura_ConLosDosGeneros_Cumple()
    {
        Assert.True(AlimentoObligatorioCalculos.Cumple(
            ModuloSeguimiento.Levante, loteEsMixto: false, Cap(h: 100m, m: 25m)));
    }

    [Fact]
    public void EngordeMixto_ConAlimentoEnElBloqueMixto_Cumple()
    {
        // El formulario en modo Mixto vuelca la captura en el bloque de hembras.
        Assert.True(AlimentoObligatorioCalculos.Cumple(
            ModuloSeguimiento.Engorde, loteEsMixto: true, Cap(h: 850m)));
    }

    [Fact]
    public void Reproductora_ConAlimento_Cumple()
    {
        Assert.True(AlimentoObligatorioCalculos.Cumple(
            ModuloSeguimiento.Reproductora, loteEsMixto: false, Cap(h: 12m)));
    }

    // ─── No cumple ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ModuloSeguimiento.Levante)]
    [InlineData(ModuloSeguimiento.Produccion)]
    [InlineData(ModuloSeguimiento.Engorde)]
    [InlineData(ModuloSeguimiento.EngordeEcuador)]
    [InlineData(ModuloSeguimiento.Reproductora)]
    public void SinNadaCargado_NoCumpleEnNingunModulo(string modulo)
    {
        Assert.False(AlimentoObligatorioCalculos.Cumple(modulo, loteEsMixto: false, Cap()));
        Assert.NotNull(AlimentoObligatorioCalculos.Motivo(modulo, false, Cap(), fecha: null));
    }

    [Fact]
    public void SoloGenerales_NoCumple()
    {
        // itemsGenerales es la bolsa de «otros ítems» (vitaminas, insumos). Si contara, un registro
        // sin una sola bolsa de alimento pasaría la validación.
        var alimento = Cap(g: 500m);

        Assert.False(AlimentoObligatorioCalculos.Cumple(ModuloSeguimiento.Levante, false, alimento));
    }

    [Fact]
    public void SoloGenerales_ElMotivoExplicaPorQueNoCuenta()
    {
        // Sin esta frase el usuario ve el formulario lleno y relee los mismos campos sin entender.
        var motivo = AlimentoObligatorioCalculos.Motivo(ModuloSeguimiento.Levante, false, Cap(g: 500m), null);

        Assert.Contains("otros ítems", motivo);
    }

    [Fact]
    public void CantidadNegativa_NoCumple()
    {
        // Guarda defensiva: una cantidad negativa no es consumo.
        Assert.False(AlimentoObligatorioCalculos.Cumple(ModuloSeguimiento.Engorde, true, Cap(h: -5m)));
    }

    // ─── Mensajes ─────────────────────────────────────────────────────────────

    [Fact]
    public void EngordeMixto_ElMotivoNombraElCampoMixto()
    {
        var motivo = AlimentoObligatorioCalculos.Motivo(
            ModuloSeguimiento.Engorde, loteEsMixto: true, Cap(), new DateOnly(2026, 8, 12));

        Assert.Contains("Mixto", motivo);
        Assert.Contains("12/08/2026", motivo);
    }

    [Fact]
    public void EngordeConSexos_ElMotivoNombraHembrasYMachos()
    {
        var motivo = AlimentoObligatorioCalculos.Motivo(
            ModuloSeguimiento.EngordeEcuador, loteEsMixto: false, Cap(), null);

        Assert.Contains("Hembras", motivo);
        Assert.Contains("Machos", motivo);
        Assert.DoesNotContain("Mixto", motivo);
    }

    [Fact]
    public void Postura_ElMotivoNombraLosDosGeneros()
    {
        var motivo = AlimentoObligatorioCalculos.Motivo(ModuloSeguimiento.Produccion, false, Cap(), null);

        Assert.Contains("Hembras", motivo);
        Assert.Contains("Machos", motivo);
    }

    [Fact]
    public void CuandoCumple_NoHayMotivo()
    {
        Assert.Null(AlimentoObligatorioCalculos.Motivo(
            ModuloSeguimiento.Levante, false, Cap(h: 1m), new DateOnly(2026, 8, 12)));
    }

    [Fact]
    public void SinFecha_ElMensajeSigueSiendoLegible()
    {
        // La carga masiva valida fila por fila y no siempre tiene la fecha resuelta.
        var motivo = AlimentoObligatorioCalculos.Motivo(ModuloSeguimiento.Levante, false, Cap(), null);

        Assert.StartsWith("El registro no tiene alimento:", motivo);
    }
}
