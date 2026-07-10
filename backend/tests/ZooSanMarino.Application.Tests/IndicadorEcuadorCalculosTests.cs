using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Fórmulas de merma de la liquidación técnica Ecuador (R1), verificadas contra el ejemplo real
/// del requerimiento (liquidación km 61 lote 02 del reporte de Costos): merma 5 und ⇒ 0,01 %,
/// ajuste −8 ⇒ −0,02 %, merma 10,66 kg descontada del total a cliente.
/// </summary>
public class IndicadorEcuadorCalculosTests
{
    // Valores del comparativo de Costos (km 61 lote 02).
    private const int MermaUnidades = 5;
    private const decimal MermaKilos = 10.66m;

    [Fact]
    public void MermaPorcentaje_EjemploRequerimiento_RedondeaA001()
    {
        // 5 aves de merma sobre ~44.700 vendidas ⇒ 0,011 % ⇒ se reporta 0,01.
        var pct = IndicadorEcuadorCalculos.MermaPorcentaje(MermaUnidades, 44_700);
        Assert.Equal(0.01m, Math.Round(pct, 2));
    }

    [Fact]
    public void AjusteAves_EjemploRequerimiento_DaMenosTres()
    {
        // encasetadas − vendidas − (mortalidad + selección) = ajuste; sin restar merma.
        var ajuste = IndicadorEcuadorCalculos.AjusteAves(
            avesEncasetadas: 45_880, avesVendidas: 44_700, mortalidad: 1_183);
        Assert.Equal(-3, ajuste);
    }

    [Fact]
    public void PorcentajeAjuste_EjemploRequerimiento_RedondeaAMenos002()
    {
        var pct = IndicadorEcuadorCalculos.PorcentajeAjuste(ajusteAves: -8, avesEncasetadas: 45_880);
        Assert.Equal(-0.02m, Math.Round(pct, 2));
    }

    [Fact]
    public void TotalKilosDespachadosCliente_DescuentaLaMermaKilos()
    {
        var total = IndicadorEcuadorCalculos.TotalKilosDespachadosCliente(135_090.66m, MermaKilos);
        Assert.Equal(135_080.00m, total);
    }

    [Fact]
    public void MermaPorcentaje_SinVentas_DevuelveCero()
    {
        Assert.Equal(0m, IndicadorEcuadorCalculos.MermaPorcentaje(5, 0));
    }

    [Fact]
    public void PorcentajeAjuste_SinEncasetadas_DevuelveCero()
    {
        Assert.Equal(0m, IndicadorEcuadorCalculos.PorcentajeAjuste(-8, 0));
    }

    [Theory]
    [InlineData("2026-03-23", "2026-05-13", 51)] // fechas del ejemplo (encaset → liquidación)
    [InlineData("2026-03-23", "2026-03-23", 0)]
    public void DiasEngorde_DiferenciaDeFechas(string encaset, string cierre, int esperado)
    {
        var dias = IndicadorEcuadorCalculos.DiasEngorde(DateTime.Parse(encaset), DateTime.Parse(cierre));
        Assert.Equal(esperado, dias);
    }

    [Fact]
    public void DiasEngorde_SinFechas_DevuelveCero()
    {
        Assert.Equal(0, IndicadorEcuadorCalculos.DiasEngorde(null, DateTime.Today));
        Assert.Equal(0, IndicadorEcuadorCalculos.DiasEngorde(DateTime.Today, null));
    }
}
