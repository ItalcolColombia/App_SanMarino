using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Golden del kardex Colombia (S3): fija el contrato aritmético que la fn SQL
/// fn_kardex_farm_inventory replica (signo por tipo de movimiento + saldo acumulado).
/// La equivalencia contra datos reales se verificó por SQL (18/18 pares Colombia, 0 diferencias);
/// estos tests fijan el signo/saldo para que un cambio de fórmula rompa aquí y avise.
/// </summary>
public class FarmInventoryKardexCalculosTests
{
    [Theory]
    [InlineData("Entry", 10, +1)]
    [InlineData("TransferIn", 10, +1)]
    [InlineData("Exit", 10, -1)]
    [InlineData("TransferOut", 10, -1)]
    [InlineData("Adjust", 10, +1)]     // quantity >= 0
    [InlineData("Adjust", 0, +1)]      // 0 => +1 (>=)
    [InlineData("Adjust", -5, -1)]     // quantity < 0
    [InlineData("Desconocido", 10, 0)] // tipo no mapeado => 0
    public void Signo_ReplicaSwitchCSharp(string tipo, decimal cantidad, decimal signoEsperado)
        => Assert.Equal(signoEsperado, FarmInventoryKardexCalculos.Signo(tipo, cantidad));

    [Fact]
    public void Delta_EntradaPositiva_SalidaNegativa()
    {
        Assert.Equal(2560m, FarmInventoryKardexCalculos.Delta("Entry", 2560m));
        Assert.Equal(-380m, FarmInventoryKardexCalculos.Delta("Exit", 380m));
    }

    [Fact]
    public void SaldosAcumulados_GoldenDatosReales_Item89()
    {
        // Golden: primeras 5 filas de farm 20 / catalog_item 89 (BD local), verificadas contra
        // la fn SQL: Entry 2560, Exit 380, Entry 1600, Exit 380, Exit 380.
        var movs = new (string, decimal)[]
        {
            ("Entry", 2560m),
            ("Exit",  380m),
            ("Entry", 1600m),
            ("Exit",  380m),
            ("Exit",  380m),
        };

        var saldos = FarmInventoryKardexCalculos.SaldosAcumulados(movs);

        Assert.Equal(new[] { 2560m, 2180m, 3780m, 3400m, 3020m }, saldos);
    }

    [Fact]
    public void Ajuste_DeltaEsSignoPorCantidad_SiempreSumaValorAbsoluto()
    {
        // Contrato histórico C#: delta = signo * quantity, y para Adjust el signo sigue al
        // propio quantity (>=0 ? +1 : -1). Por eso un Adjust con quantity negativa produce un
        // delta POSITIVO (== |quantity|). Se replica tal cual (no es un bug: es el modelo previo).
        Assert.Equal(+30m, FarmInventoryKardexCalculos.Delta("Adjust", -30m));
        Assert.Equal(+20m, FarmInventoryKardexCalculos.Delta("Adjust", 20m));

        var movs = new (string, decimal)[]
        {
            ("Entry",  100m),
            ("Adjust", -30m),  // delta = +30
            ("Adjust",  20m),  // delta = +20
        };
        Assert.Equal(new[] { 100m, 130m, 150m }, FarmInventoryKardexCalculos.SaldosAcumulados(movs));
    }

    [Fact]
    public void SaldosAcumulados_Vacio_SinFilas()
        => Assert.Empty(FarmInventoryKardexCalculos.SaldosAcumulados(Array.Empty<(string, decimal)>()));
}
