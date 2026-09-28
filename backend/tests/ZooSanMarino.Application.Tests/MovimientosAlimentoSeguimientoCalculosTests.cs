using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

public class MovimientosAlimentoSeguimientoCalculosTests
{
    [Theory]
    [InlineData(true, false, false, MovimientosAlimentoSeguimientoCalculos.AlcanceInventarioAlimento.Silo)]
    [InlineData(false, true, false, MovimientosAlimentoSeguimientoCalculos.AlcanceInventarioAlimento.Galpon)]
    [InlineData(false, null, true, MovimientosAlimentoSeguimientoCalculos.AlcanceInventarioAlimento.Galpon)]
    [InlineData(false, false, true, MovimientosAlimentoSeguimientoCalculos.AlcanceInventarioAlimento.Granja)]
    [InlineData(false, null, false, MovimientosAlimentoSeguimientoCalculos.AlcanceInventarioAlimento.Granja)]
    public void ResolverAlcance_RespetaSiloYOverrideDeGranja(
        bool porSilo,
        bool? granjaPorGalpon,
        bool empresaPorGalpon,
        MovimientosAlimentoSeguimientoCalculos.AlcanceInventarioAlimento esperado)
    {
        Assert.Equal(esperado, MovimientosAlimentoSeguimientoCalculos.ResolverAlcance(
            porSilo, granjaPorGalpon, empresaPorGalpon));
    }

    [Fact]
    public void ResolverRango_FaseAbierta_LlegaHastaHoy()
    {
        var rango = MovimientosAlimentoSeguimientoCalculos.ResolverRango(
            new DateTime(2026, 9, 1), null,
            new DateTime(2026, 9, 2), new DateTime(2026, 9, 20),
            faseCerrada: false, hoy: new DateTime(2026, 9, 24));

        Assert.Equal(new DateTime(2026, 9, 1), rango!.Desde);
        Assert.Equal(new DateTime(2026, 9, 24), rango.Hasta);
    }

    [Fact]
    public void ResolverRango_FaseCerrada_NoIncluyeDiasPosterioresAlUltimoSeguimiento()
    {
        var rango = MovimientosAlimentoSeguimientoCalculos.ResolverRango(
            new DateTime(2026, 8, 1), null,
            new DateTime(2026, 8, 1), new DateTime(2026, 9, 10),
            faseCerrada: true, hoy: new DateTime(2026, 9, 24));

        Assert.Equal(new DateTime(2026, 9, 10), rango!.Hasta);
    }

    [Fact]
    public void ResolverRango_IntersectaFiltrosYDevuelveNullSiNoSeCruzan()
    {
        var visible = MovimientosAlimentoSeguimientoCalculos.ResolverRango(
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 30),
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 30),
            faseCerrada: true, hoy: new DateTime(2026, 9, 24),
            filtroDesde: new DateTime(2026, 9, 10), filtroHasta: new DateTime(2026, 9, 15));
        var vacio = MovimientosAlimentoSeguimientoCalculos.ResolverRango(
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 5),
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 5),
            faseCerrada: true, hoy: new DateTime(2026, 9, 24),
            filtroDesde: new DateTime(2026, 9, 10));

        Assert.Equal(new DateTime(2026, 9, 10), visible!.Desde);
        Assert.Equal(new DateTime(2026, 9, 15), visible.Hasta);
        Assert.Null(vacio);
    }

    [Fact]
    public void ResolverRango_NoExpandeLosLimitesDeLaFasePorSeguimientosInconsistentes()
    {
        var rango = MovimientosAlimentoSeguimientoCalculos.ResolverRango(
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 30),
            new DateTime(2026, 8, 31), new DateTime(2026, 10, 1),
            faseCerrada: true, hoy: new DateTime(2026, 9, 24));

        Assert.Equal(new DateTime(2026, 9, 1), rango!.Desde);
        Assert.Equal(new DateTime(2026, 9, 30), rango.Hasta);
    }

    [Fact]
    public void ResolverRango_SinInicioNiSeguimiento_DevuelveNull()
    {
        Assert.Null(MovimientosAlimentoSeguimientoCalculos.ResolverRango(
            null, null, null, null, faseCerrada: false, hoy: new DateTime(2026, 9, 24)));
    }
}
