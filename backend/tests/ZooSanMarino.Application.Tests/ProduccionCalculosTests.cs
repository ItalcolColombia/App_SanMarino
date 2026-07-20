using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de las fórmulas puras de producción (REQ-004) que la fn SQL replica.
/// Sirven de contrato numérico: si alguien cambia la fórmula en C#, estos tests fallan y avisan
/// que la fn SQL debe alinearse (y viceversa). Valores verificados contra P-K345A semana 26.
/// </summary>
public class ProduccionCalculosTests
{
    [Fact]
    public void PorcentajeProduccion_HenDay_SoloHembras()
    {
        // P-K345A sem26: promedio 1720.5714285714287 huevos/día, 7597 hembras vivas.
        var res = ProduccionCalculos.PorcentajeProduccion(1720.5714285714287m, 7597);
        Assert.Equal(22.648037759265875m, res, 10);
    }

    [Fact]
    public void PorcentajeProduccion_SinHembras_Cero()
        => Assert.Equal(0m, ProduccionCalculos.PorcentajeProduccion(100m, 0));

    [Fact]
    public void Htaa_AcumuladoPorAveAlojada()
    {
        // P-K345A sem26: cum huevos totales 12044, 7597 hembras iniciales.
        var res = ProduccionCalculos.Htaa(12044, 7597);
        Assert.Equal(1.5853626431486112m, res, 10);
    }

    [Fact]
    public void Hiaa_AcumuladoPorAveAlojada()
    {
        var res = ProduccionCalculos.Hiaa(7905, 7597);
        Assert.Equal(1.0405423193365801m, res, 10);
    }

    [Fact]
    public void Htaa_SinAvesIniciales_Cero()
        => Assert.Equal(0m, ProduccionCalculos.Htaa(1000, 0));

    [Fact]
    public void GramosAveDia_ConsumoDiarioPromedio()
    {
        // (consumoKg * 1000 / dias) / aves  ==  7482 * 1000 / 7 / 7597
        var esperado = 7482m * 1000m / 7m / 7597m;
        var res = ProduccionCalculos.GramosAveDia(7482m, 7597, 7);
        Assert.Equal(esperado, res, 10);
    }

    [Fact]
    public void GramosAveDia_SinAves_Cero()
        => Assert.Equal(0m, ProduccionCalculos.GramosAveDia(100m, 0, 7));

    // ── REQ-004: %Retiro real (la fn SQL replica esta misma aritmética en retiro_sem_*/retiro_ac_*) ──

    [Fact]
    public void PorcentajeRetiroSemanal_MortMasSelSobreSaldo()
    {
        // (5 + 2) / 7000 * 100
        var esperado = 7m / 7000m * 100m;
        Assert.Equal(esperado, ProduccionCalculos.PorcentajeRetiroSemanal(5, 2, 7000), 10);
    }

    [Fact]
    public void PorcentajeRetiroSemanal_SinSaldo_Cero()
        => Assert.Equal(0m, ProduccionCalculos.PorcentajeRetiroSemanal(3, 1, 0));

    [Fact]
    public void PorcentajeRetiroAcumulado_AcumSobreIniciales()
    {
        // (40 + 10) / 7600 * 100
        var esperado = 50m / 7600m * 100m;
        Assert.Equal(esperado, ProduccionCalculos.PorcentajeRetiroAcumulado(40, 10, 7600), 10);
    }

    [Fact]
    public void PorcentajeRetiroAcumulado_SinIniciales_Cero()
        => Assert.Equal(0m, ProduccionCalculos.PorcentajeRetiroAcumulado(50, 0, 0));
}
