using ZooSanMarino.Application.Calculos;
using Ubicacion = ZooSanMarino.Application.Calculos.MovimientoPolloEngordeCalculos.UbicacionMovimiento;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Ubicación DESTINO efectiva de un movimiento de pollo engorde: desde que el traslado puede apuntar a
/// otra granja/galpón, el front manda la ubicación explícita; los flujos históricos siguen mandando solo
/// el lote y la cascada del modal permite elegir la granja sin bajar a galpón. El contrato es
/// <b>campo por campo</b>: lo explícito manda, lo que falte se deriva del lote destino, y sin lote destino
/// (venta / retiro / ajuste) no se inventa nada — el comportamiento previo queda intacto.
/// </summary>
public class MovimientoPolloEngordeDestinoCalculosTests
{
    private static readonly Ubicacion LoteEnGranja7 = new(7, "NUC-B", "GALP-9");

    [Fact]
    public void UbicacionExplicitaCompleta_NoSePisaConLaDelLote()
    {
        var explicita = new Ubicacion(3, "NUC-A", "GALP-1");

        var r = MovimientoPolloEngordeCalculos.ResolverUbicacionDestino(explicita, LoteEnGranja7);

        Assert.Equal(3, r.GranjaId);
        Assert.Equal("NUC-A", r.NucleoId);
        Assert.Equal("GALP-1", r.GalponId);
    }

    [Fact]
    public void GranjaExplicitaSinNucleoNiGalpon_CompletaNucleoYGalponDelLote()
    {
        // Caso real de la cascada del modal: el usuario elige granja y lote pero no baja a galpón. Las
        // aves aterrizan igual en el galpón del lote destino, así que el movimiento lo registra.
        var r = MovimientoPolloEngordeCalculos.ResolverUbicacionDestino(new Ubicacion(3, null, null), LoteEnGranja7);

        Assert.Equal(3, r.GranjaId);        // la granja explícita NO se pisa
        Assert.Equal("NUC-B", r.NucleoId);  // lo que faltaba sí se completa
        Assert.Equal("GALP-9", r.GalponId);
    }

    [Fact]
    public void SinGranjaExplicita_SeDerivaTodoDelLoteDestino()
    {
        var r = MovimientoPolloEngordeCalculos.ResolverUbicacionDestino(new Ubicacion(null, null, null), LoteEnGranja7);

        Assert.Equal(7, r.GranjaId);
        Assert.Equal("NUC-B", r.NucleoId);
        Assert.Equal("GALP-9", r.GalponId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SinGranjaExplicita_NucleoVacioSeCompletaDesdeElLote(string? nucleoExplicito)
    {
        var r = MovimientoPolloEngordeCalculos.ResolverUbicacionDestino(
            new Ubicacion(null, nucleoExplicito, null), LoteEnGranja7);

        Assert.Equal("NUC-B", r.NucleoId);
        Assert.Equal("GALP-9", r.GalponId);
    }

    [Fact]
    public void SinGranjaExplicita_NucleoYGalponPropiosSobrevivenAlRelleno()
    {
        var r = MovimientoPolloEngordeCalculos.ResolverUbicacionDestino(
            new Ubicacion(null, "NUC-X", "GALP-X"), LoteEnGranja7);

        Assert.Equal(7, r.GranjaId);        // la granja sí se deriva: no venía
        Assert.Equal("NUC-X", r.NucleoId);  // lo que sí venía, se respeta
        Assert.Equal("GALP-X", r.GalponId);
    }

    [Fact]
    public void GalponExplicitoSinNucleo_CompletaSoloElNucleo()
    {
        var r = MovimientoPolloEngordeCalculos.ResolverUbicacionDestino(
            new Ubicacion(3, null, "GALP-X"), LoteEnGranja7);

        Assert.Equal(3, r.GranjaId);
        Assert.Equal("NUC-B", r.NucleoId);   // completado desde el lote
        Assert.Equal("GALP-X", r.GalponId);  // el explícito manda
    }

    [Fact]
    public void SinLoteDestino_QuedaTalCualLlego()
    {
        // Venta / retiro / ajuste: no hay lote destino del cual derivar. Comportamiento previo byte a byte.
        var r = MovimientoPolloEngordeCalculos.ResolverUbicacionDestino(new Ubicacion(null, null, null), null);

        Assert.Null(r.GranjaId);
        Assert.Null(r.NucleoId);
        Assert.Null(r.GalponId);
    }

    [Fact]
    public void SinLoteDestino_ConservaLaUbicacionExplicitaParcial()
    {
        var r = MovimientoPolloEngordeCalculos.ResolverUbicacionDestino(new Ubicacion(null, "NUC-A", null), null);

        Assert.Null(r.GranjaId);
        Assert.Equal("NUC-A", r.NucleoId);
        Assert.Null(r.GalponId);
    }

    [Fact]
    public void LoteDestinoSinNucleoNiGalpon_SoloAportaLaGranja()
    {
        var r = MovimientoPolloEngordeCalculos.ResolverUbicacionDestino(
            new Ubicacion(null, null, null), new Ubicacion(11, null, null));

        Assert.Equal(11, r.GranjaId);
        Assert.Null(r.NucleoId);
        Assert.Null(r.GalponId);
    }
}
