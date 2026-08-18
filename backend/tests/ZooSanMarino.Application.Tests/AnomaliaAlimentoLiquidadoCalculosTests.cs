using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Señalamiento de la anomalía R2 (Fase 3): qué se dice de un lote que se liquidó dejando alimento en
/// el galpón. La regla operativa es que al liquidar el galpón queda en cero y el sobrante se traslada;
/// estos tests fijan cuándo eso se cumplió, cuándo quedó pendiente y cuándo la foto congelada reclama
/// kilos que ya no existen.
/// </summary>
public class AnomaliaAlimentoLiquidadoCalculosTests
{
    // ─── T1 · el procedimiento se cumplió: el sobrante salió por traslado ──────

    [Fact]
    public void T1_SobranteTrasladado_NoQuedaNadaNiAnomalia()
    {
        Assert.Equal(0m, AnomaliaAlimentoLiquidadoCalculos.KgSinTrasladar(3000m, 3000m));
        Assert.Equal(0m, AnomaliaAlimentoLiquidadoCalculos.KgSinRespaldo(3000m, 3000m, 0m));
        Assert.Equal(EstadoAlimentoLiquidado.Trasladado,
                     AnomaliaAlimentoLiquidadoCalculos.Clasificar(3000m, 3000m, 0m));
    }

    // ─── T2 · quedó en el galpón y el stock lo respalda ───────────────────────

    [Fact]
    public void T2_QuedaEnElGalponConStockQueLoRespalda_EsPendiente()
    {
        Assert.Equal(3000m, AnomaliaAlimentoLiquidadoCalculos.KgSinTrasladar(3000m, 0m));
        Assert.Equal(0m,    AnomaliaAlimentoLiquidadoCalculos.KgSinRespaldo(3000m, 0m, 5000m));
        Assert.Equal(EstadoAlimentoLiquidado.PendienteEnGalpon,
                     AnomaliaAlimentoLiquidadoCalculos.Clasificar(3000m, 0m, 5000m));
    }

    // ─── T3 · el caso real 43/G0055: la foto reclama más de lo que hay ────────

    [Fact]
    public void T3_G0055_LaFotoReclamaMasKilosDeLosQueTieneElGalpon()
    {
        // Lote 86, liquidado el 30-jul-2026 con 15.540 kg congelados; el galpón tiene hoy 9.980.
        Assert.Equal(15540m, AnomaliaAlimentoLiquidadoCalculos.KgSinTrasladar(15540m, 0m));
        Assert.Equal(5560m,  AnomaliaAlimentoLiquidadoCalculos.KgSinRespaldo(15540m, 0m, 9980m));
        Assert.Equal(EstadoAlimentoLiquidado.SinRespaldoFisico,
                     AnomaliaAlimentoLiquidadoCalculos.Clasificar(15540m, 0m, 9980m));
    }

    // ─── T4 · sin traslado y sin stock: todos los kilos son fantasma ──────────

    [Fact]
    public void T4_SinTrasladoYSinStock_TodoQuedaSinRespaldo()
    {
        Assert.Equal(3000m, AnomaliaAlimentoLiquidadoCalculos.KgSinRespaldo(3000m, 0m, 0m));
        Assert.Equal(EstadoAlimentoLiquidado.SinRespaldoFisico,
                     AnomaliaAlimentoLiquidadoCalculos.Clasificar(3000m, 0m, 0m));
    }

    // ─── T5 · la tolerancia es la misma del cuadre (1 kg) ─────────────────────

    [Theory]
    [InlineData(2999.5)]
    [InlineData(2999)]
    public void T5_DentroDeLaTolerancia_CuentaComoTrasladado(decimal salidas)
        => Assert.Equal(EstadoAlimentoLiquidado.Trasladado,
                        AnomaliaAlimentoLiquidadoCalculos.Clasificar(3000m, salidas, 0m));

    [Fact]
    public void T5b_ToleranciaEsLaMismaQueLaDelCuadre()
        => Assert.Equal(CuadreAlimentoEngordeCalculos.ToleranciaKg,
                        AnomaliaAlimentoLiquidadoCalculos.ToleranciaKg);

    [Fact]
    public void T5c_UnKiloYMedioSobreLaTolerancia_YaEsPendiente()
        => Assert.Equal(EstadoAlimentoLiquidado.PendienteEnGalpon,
                        AnomaliaAlimentoLiquidadoCalculos.Clasificar(3000m, 2998.5m, 100m));

    // ─── T6 · salió más de lo que decía la foto: nunca queda en negativo ──────

    [Fact]
    public void T6_SalioMasDeLoQueDeciaLaFoto_NoQuedaNegativo()
    {
        Assert.Equal(0m, AnomaliaAlimentoLiquidadoCalculos.KgSinTrasladar(3000m, 4000m));
        Assert.Equal(0m, AnomaliaAlimentoLiquidadoCalculos.KgSinRespaldo(3000m, 4000m, 0m));
        Assert.Equal(EstadoAlimentoLiquidado.Trasladado,
                     AnomaliaAlimentoLiquidadoCalculos.Clasificar(3000m, 4000m, 0m));
    }

    [Fact]
    public void T6b_StockNegativoNoSumaRespaldo()
        => Assert.Equal(3000m, AnomaliaAlimentoLiquidadoCalculos.KgSinRespaldo(3000m, 0m, -500m));

    // ─── T7 · el texto dice qué pasó y qué hacer ──────────────────────────────

    [Fact]
    public void T7_Describir_Trasladado_NoPideNada()
        => Assert.Equal("El sobrante salió del galpón por traslado.",
                        AnomaliaAlimentoLiquidadoCalculos.Describir(3000m, 3000m, 0m));

    [Fact]
    public void T7b_Describir_Pendiente_NombraLosKilosYPideTrasladarlos()
    {
        var texto = AnomaliaAlimentoLiquidadoCalculos.Describir(3000m, 0m, 5000m);
        Assert.Contains("3,000.0 kg", texto);
        Assert.Contains("Trasladarlos", texto);
    }

    [Fact]
    public void T7c_Describir_SinRespaldo_NombraLosKilosQueYaNoEstan()
    {
        var texto = AnomaliaAlimentoLiquidadoCalculos.Describir(15540m, 0m, 9980m);
        Assert.Contains("15,540.0 kg", texto);   // lo que la foto reclama
        Assert.Contains("9,980.0 kg", texto);    // lo que el galpón tiene
        Assert.Contains("5,560.0 kg", texto);    // lo que ya consumió otro ciclo
    }

    // ─── T8 · el orden de severidad es el que ordena la pantalla ──────────────

    [Fact]
    public void T8_OrdenDeSeveridad_TrasladadoEsElMenosGrave()
    {
        Assert.True((int)EstadoAlimentoLiquidado.Trasladado
                  < (int)EstadoAlimentoLiquidado.PendienteEnGalpon);
        Assert.True((int)EstadoAlimentoLiquidado.PendienteEnGalpon
                  < (int)EstadoAlimentoLiquidado.SinRespaldoFisico);
    }
}
