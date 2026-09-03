using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de la disponibilidad de un lote de postura. Los números de los casos «medido» salen de
/// la copia local del 2-sep-2026 y son los que motivaron el arreglo.
/// </summary>
public class DisponibilidadLoteCalculosTests
{
    // ── BajasEtapa ───────────────────────────────────────────────────────────────

    [Fact]
    public void BajasEtapa_SumaLosTresComponentes()
    {
        // Es la composicion canonica (SaldoAvesLevanteCalculos.BajasNetas) sin los terminos de
        // traslado y venta, que en este service llegan por movimiento_aves.
        Assert.Equal(6, DisponibilidadLoteCalculos.BajasEtapa(mortalidad: 1, seleccion: 2, errorSexaje: 3));
        Assert.Equal(0, DisponibilidadLoteCalculos.BajasEtapa(0, 0, 0));
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, 0, 1)]
    public void BajasEtapa_NingunTerminoQuedaSinSumar(int mort, int sel, int err) =>
        Assert.Equal(1, DisponibilidadLoteCalculos.BajasEtapa(mort, sel, err));

    // ── AvesVivas ────────────────────────────────────────────────────────────────

    [Fact]
    public void AvesVivas_SinProduccion_SoloDescuentaLevanteYRetiros()
    {
        var vivas = DisponibilidadLoteCalculos.AvesVivas(
            iniciales: 10_000, bajasLevante: 250, bajasProduccion: 0, retiros: 300);

        Assert.Equal(9_450, vivas);
    }

    [Fact]
    public void AvesVivas_ElLoteDespobladoYaNoInformaAvesQueNoTiene()
    {
        // Medido en el lote 14 (LPP 6): la formula vieja informaba 10.748 hembras disponibles
        // porque solo restaba la mortalidad de levante. Con la seleccion de produccion
        // (9.686) y su mortalidad (738) quedan 324, que es lo que el lote tiene de verdad.
        const int hembrasTrasLevanteYRetiros = 10_748;
        var bajasProduccion = DisponibilidadLoteCalculos.BajasEtapa(
            mortalidad: 738, seleccion: 9_686, errorSexaje: 0);

        var vivas = DisponibilidadLoteCalculos.AvesVivas(
            iniciales: hembrasTrasLevanteYRetiros, bajasLevante: 0, bajasProduccion: bajasProduccion, retiros: 0);

        Assert.Equal(10_424, bajasProduccion);
        Assert.Equal(324, vivas);
    }

    [Theory]
    // iniciales, bajasLevante, bajasProduccion, retiros, esperado
    [InlineData(100, 0, 0, 0, 100)]     // nada salio
    [InlineData(100, 100, 0, 0, 0)]     // se murio todo: cero, no negativo
    [InlineData(100, 60, 30, 40, 0)]    // sobre-descuento: se recorta en cero
    [InlineData(0, 0, 0, 0, 0)]         // lote sin aves
    public void AvesVivas_NuncaEsNegativa(
        int iniciales, int bajasLev, int bajasProd, int retiros, int esperado)
    {
        var vivas = DisponibilidadLoteCalculos.AvesVivas(iniciales, bajasLev, bajasProd, retiros);

        Assert.Equal(esperado, vivas);
        Assert.True(vivas >= 0);
    }

    [Fact]
    public void AvesVivas_LosTresDescuentosPesanIgual()
    {
        // Que ningun termino quede sin restar por un typo.
        Assert.Equal(999, DisponibilidadLoteCalculos.AvesVivas(1_000, 1, 0, 0));
        Assert.Equal(999, DisponibilidadLoteCalculos.AvesVivas(1_000, 0, 1, 0));
        Assert.Equal(999, DisponibilidadLoteCalculos.AvesVivas(1_000, 0, 0, 1));
    }

    // ── InformaHuevos ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, false)]   // sin LPP no hay producción de la que hablar
    [InlineData(0, false)]      // el default de un int? mal resuelto no cuenta como LPP
    [InlineData(-1, false)]
    [InlineData(6, true)]       // medido: LPP 6 = lote 14
    public void InformaHuevos_SoloConLppViva(int? lpp, bool esperado) =>
        Assert.Equal(esperado, DisponibilidadLoteCalculos.InformaHuevos(lpp));

    // ── La regla de fase sigue siendo la canónica, no una nueva ──────────────────

    [Theory]
    // levanteCerrado, tieneProduccion, faseEsperada
    [InlineData(true, true, "Produccion")]
    [InlineData(true, false, "Levante")]   // levante cerrado sin producción sigue siendo levante
    [InlineData(false, true, "Levante")]   // producción con levante abierto: dato a medio cargar
    [InlineData(false, false, "Levante")]
    public void TipoLote_SaleDeLaReglaCanonica(bool levanteCerrado, bool tieneProduccion, string esperada) =>
        Assert.Equal(esperada, FaseLoteCalculos.ResolverFaseVisible(levanteCerrado, tieneProduccion));

    [Fact]
    public void TipoLote_LosCasosMedidos()
    {
        // 13 y 14: levante Cerrado + LPP viva ⇒ Producción (hoy decían Levante y escondían 3,6 M huevos).
        Assert.Equal("Produccion", FaseLoteCalculos.ResolverFaseVisible(levanteCerrado: true, tieneProduccion: true));
        // 114 y 115: levante Abierto y SIN LPP ⇒ Levante (hoy decían Producción y bloqueaban 35.372 aves).
        Assert.Equal("Levante", FaseLoteCalculos.ResolverFaseVisible(levanteCerrado: false, tieneProduccion: false));
    }
}
