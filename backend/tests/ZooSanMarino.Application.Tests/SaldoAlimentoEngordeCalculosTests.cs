using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Verifica TryGetHistDeltaAndOrd (SaldoAlimentoEngordeCalculos), el cálculo puro compartido
/// que usa SeguimientoAvesEngordeEcuadorService.SaldoAlimento y SeguimientoAvesEngordeService
/// (Colombia) para el recálculo del saldo de alimento por lote: qué eventos del histórico
/// unificado participan, su signo y su orden intra-día.
/// </summary>
public class SaldoAlimentoEngordeCalculosTests
{
    private static LoteRegistroHistoricoUnificado Hist(string tipoEvento, decimal? cantidadKg, bool anulado = false) =>
        new()
        {
            TipoEvento = tipoEvento,
            OrigenTabla = "origen",
            FechaOperacion = new DateTime(2026, 1, 1),
            CantidadKg = cantidadKg,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Anulado = anulado,
        };

    [Fact]
    public void InvIngreso_SumaPositivoConOrdenCero()
    {
        var ok = SaldoAlimentoEngordeCalculos.TryGetHistDeltaAndOrd(Hist("INV_INGRESO", 50m), out var delta, out var ord);
        Assert.True(ok);
        Assert.Equal(50m, delta);
        Assert.Equal(0, ord);
    }

    [Fact]
    public void InvTrasladoEntrada_SumaPositivoConOrdenUno()
    {
        var ok = SaldoAlimentoEngordeCalculos.TryGetHistDeltaAndOrd(Hist("INV_TRASLADO_ENTRADA", 30m), out var delta, out var ord);
        Assert.True(ok);
        Assert.Equal(30m, delta);
        Assert.Equal(1, ord);
    }

    [Fact]
    public void InvTrasladoSalida_RestaValorAbsolutoConOrdenDos()
    {
        // Aunque venga en negativo, el delta siempre resta el valor absoluto.
        var ok = SaldoAlimentoEngordeCalculos.TryGetHistDeltaAndOrd(Hist("INV_TRASLADO_SALIDA", -20m), out var delta, out var ord);
        Assert.True(ok);
        Assert.Equal(-20m, delta);
        Assert.Equal(2, ord);
    }

    [Theory]
    [InlineData("INV_INGRESO")]
    [InlineData("INV_TRASLADO_ENTRADA")]
    [InlineData("INV_TRASLADO_SALIDA")]
    public void KgEnCero_NoParticipa(string tipoEvento)
    {
        var ok = SaldoAlimentoEngordeCalculos.TryGetHistDeltaAndOrd(Hist(tipoEvento, 0m), out var delta, out var ord);
        Assert.False(ok);
        Assert.Equal(0m, delta);
        Assert.Equal(0, ord);
    }

    [Fact]
    public void CantidadKgNula_SeTrataComoCero_NoParticipa()
    {
        var ok = SaldoAlimentoEngordeCalculos.TryGetHistDeltaAndOrd(Hist("INV_INGRESO", null), out _, out _);
        Assert.False(ok);
    }

    [Theory]
    [InlineData("VENTA_AVES")]
    [InlineData("INV_CONSUMO")]
    [InlineData("OTRO")]
    public void TipoEventoNoRelevante_NoParticipa(string tipoEvento)
    {
        var ok = SaldoAlimentoEngordeCalculos.TryGetHistDeltaAndOrd(Hist(tipoEvento, 100m), out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void EventoAnulado_NoParticipaAunqueSeaDeUnTipoRelevante()
    {
        var ok = SaldoAlimentoEngordeCalculos.TryGetHistDeltaAndOrd(Hist("INV_INGRESO", 100m, anulado: true), out var delta, out var ord);
        Assert.False(ok);
        Assert.Equal(0m, delta);
        Assert.Equal(0, ord);
    }
}
