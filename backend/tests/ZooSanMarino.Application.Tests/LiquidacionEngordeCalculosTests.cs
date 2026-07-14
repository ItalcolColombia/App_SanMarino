using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Verifica que LiquidacionEngordeCalculos reproduce exactamente la aritmética
/// que vivía duplicada en SeguimientoAvesEngordeService (Colombia) y
/// SeguimientoAvesEngordeEcuadorService (Ecuador).
/// </summary>
public class LiquidacionEngordeCalculosTests
{
    [Fact]
    public void AvesInicio_ConRegistroInicio_UsaElHistorial()
    {
        var r = LiquidacionEngordeCalculos.CalcularAvesInicio(
            tieneRegistroInicio: true, iniHembras: 100, iniMachos: 50, iniMixtas: 10,
            loteHembras: 999, loteMachos: 999, loteMixtas: 999, avesEncasetadas: 999);
        Assert.Equal((100, 50, 10), r);
    }

    [Fact]
    public void AvesInicio_SinHistorial_UsaSaldosDelLote()
    {
        var r = LiquidacionEngordeCalculos.CalcularAvesInicio(
            false, 0, 0, 0, loteHembras: 200, loteMachos: 100, loteMixtas: 5, avesEncasetadas: 999);
        Assert.Equal((200, 100, 5), r);
    }

    [Fact]
    public void AvesInicio_SinHistorialNiSaldos_CaeAEncasetadasComoMixtas()
    {
        var r = LiquidacionEngordeCalculos.CalcularAvesInicio(
            false, 0, 0, 0, loteHembras: 0, loteMachos: null, loteMixtas: 0, avesEncasetadas: 1234);
        Assert.Equal((0, 0, 1234), r);
    }

    [Fact]
    public void AvesInicio_SinNada_TodoCero()
    {
        var r = LiquidacionEngordeCalculos.CalcularAvesInicio(
            false, 0, 0, 0, null, null, null, null);
        Assert.Equal((0, 0, 0), r);
    }

    [Fact]
    public void AvesInicio_EncasetadasNoAplicaSiHaySaldos()
    {
        // Con algún saldo > 0 no se usa el fallback de encasetadas.
        var r = LiquidacionEngordeCalculos.CalcularAvesInicio(
            false, 0, 0, 0, loteHembras: 1, loteMachos: 0, loteMixtas: 0, avesEncasetadas: 1000);
        Assert.Equal((1, 0, 0), r);
    }

    [Theory]
    [InlineData(1000, 0, 100, 200, 700)]
    [InlineData(100, 0, 80, 50, 0)]   // nunca negativo
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(500, 0, 0, 0, 500)]
    // Caso real: lote 77 "2603" (Sacachun 3b, galpón G0049) — mort_caja_h=17 sin registrar en la
    // tabla diaria dejaba "17 aves vivas" fantasma mientras el widget "Aves disponibles" (que sí
    // resta mort_caja del maestro) mostraba 0/0. Ver fn_seguimiento_diario_engorde v8.
    [InlineData(20121, 17, 2357, 17747, 0)]
    public void AvesVivas_InicioMenosMortCajaMenosBajasMenosVentas_NuncaNegativo(
        int totalInicio, int mortCajaTotal, int bajas, int ventas, int esperado)
    {
        Assert.Equal(esperado, LiquidacionEngordeCalculos.CalcularAvesVivas(totalInicio, mortCajaTotal, bajas, ventas));
    }
}
