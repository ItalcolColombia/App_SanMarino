using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Verifica que SeguimientoEngordeCalculos reproduce exactamente los helpers
/// que estaban duplicados en los servicios de seguimiento engorde de Colombia y Ecuador.
/// </summary>
public class SeguimientoEngordeCalculosTests
{
    [Theory]
    [InlineData(10.0, 3.2, 0.19, 32.0, 1.9)]
    [InlineData(0.0, 3.2, 0.19, 0.0, 0.0)]
    [InlineData(1.2345, 3.0, 0.2, 3.704, 0.247)] // redondeo a 3 decimales (Math.Round banker's)
    public void CalcularDerivados_MultiplicaYRedondeaA3(double consumo, double kcal, double prot, double kcalEsp, double protEsp)
    {
        var (k, p) = SeguimientoEngordeCalculos.CalcularDerivados(consumo, kcal, prot);
        Assert.Equal(kcalEsp, k);
        Assert.Equal(protEsp, p);
    }

    [Fact]
    public void CalcularDerivados_NulosSePropagan()
    {
        var (k, p) = SeguimientoEngordeCalculos.CalcularDerivados(10, null, 0.19);
        Assert.Null(k);
        Assert.Equal(1.9, p);

        (k, p) = SeguimientoEngordeCalculos.CalcularDerivados(10, 3.2, null);
        Assert.Equal(32.0, k);
        Assert.Null(p);
    }

    [Theory]
    [InlineData(0, 1)]   // mismo día → semana 1
    [InlineData(6, 1)]   // día 6 → semana 1
    [InlineData(7, 2)]   // día 7 → semana 2
    [InlineData(13, 2)]
    [InlineData(14, 3)]
    [InlineData(-5, 1)]  // registro antes del encaset → mínimo semana 1
    public void CalcularSemana_UnBased_PisoEnUno(int diasDespues, int semanaEsperada)
    {
        var encaset = new DateTime(2026, 1, 1);
        var registro = encaset.AddDays(diasDespues);
        Assert.Equal(semanaEsperada, SeguimientoEngordeCalculos.CalcularSemana(encaset, registro));
    }
}
