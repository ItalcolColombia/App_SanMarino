using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Fase D del plan de silos — los reportes Contable y Técnico leen el alimento del módulo de
/// inventario que la EMPRESA declare.
///
/// <para>
/// El contrato que fijan estos tests: con el flag apagado no se lee nada nuevo (el reporte queda
/// como está), y con el flag puesto cada <c>movement_type</c> del módulo unificado cae en UNA sola
/// de las tres categorías del reporte — un tipo que caiga en dos duplicaría kilos en un reporte
/// contable.
/// </para>
/// </summary>
public class ReporteAlimentoInventarioCalculosTests
{
    [Fact]
    public void FlagApagado_NoLeeElModuloUnificado()
    {
        Assert.False(ReporteAlimentoInventarioCalculos.LeeInventarioUnificado(false));
    }

    [Fact]
    public void FlagEncendido_LeeElModuloUnificado()
    {
        Assert.True(ReporteAlimentoInventarioCalculos.LeeInventarioUnificado(true));
    }

    [Theory]
    [InlineData("Ingreso")]
    [InlineData("TrasladoEntrada")]
    [InlineData("TrasladoInterGranjaEntrada")]
    public void Entradas(string tipo)
    {
        Assert.Equal(CategoriaMovimientoAlimento.Entrada, ReporteAlimentoInventarioCalculos.Categoria(tipo));
    }

    [Theory]
    [InlineData("TrasladoSalida")]
    [InlineData("TrasladoInterGranjaSalida")]
    [InlineData("TrasladoInterGranjaPendiente")]
    public void Traslados(string tipo)
    {
        Assert.Equal(CategoriaMovimientoAlimento.Traslado, ReporteAlimentoInventarioCalculos.Categoria(tipo));
    }

    [Fact]
    public void Consumo_EsRetiro()
    {
        Assert.Equal(CategoriaMovimientoAlimento.Retiro, ReporteAlimentoInventarioCalculos.Categoria("Consumo"));
    }

    [Theory]
    [InlineData("AjusteStock")]
    [InlineData("EliminacionStock")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("loQueSea")]
    public void AjustesYDesconocidos_NoEntranAlReporte(string? tipo)
    {
        // Mueven el saldo pero no son operación: el reporte viejo tampoco los mostraba, y sumarlos a
        // las entradas inflaría el kardex con correcciones de digitación.
        Assert.Equal(CategoriaMovimientoAlimento.Ninguna, ReporteAlimentoInventarioCalculos.Categoria(tipo));
    }

    [Fact]
    public void CadaTipoCaeEnUnaSolaCategoria()
    {
        // Un tipo que estuviera en dos listas duplicaría kilos en el reporte.
        var todas = ReporteAlimentoInventarioCalculos.TiposEntrada
            .Concat(ReporteAlimentoInventarioCalculos.TiposTraslado)
            .Concat(ReporteAlimentoInventarioCalculos.TiposRetiro)
            .ToArray();

        Assert.Equal(todas.Length, todas.Distinct().Count());
        // Y las listas coinciden con lo que dice Categoria(), que es lo que traduce el service.
        foreach (var t in ReporteAlimentoInventarioCalculos.TiposEntrada)
            Assert.Equal(CategoriaMovimientoAlimento.Entrada, ReporteAlimentoInventarioCalculos.Categoria(t));
        foreach (var t in ReporteAlimentoInventarioCalculos.TiposTraslado)
            Assert.Equal(CategoriaMovimientoAlimento.Traslado, ReporteAlimentoInventarioCalculos.Categoria(t));
        foreach (var t in ReporteAlimentoInventarioCalculos.TiposRetiro)
            Assert.Equal(CategoriaMovimientoAlimento.Retiro, ReporteAlimentoInventarioCalculos.Categoria(t));
    }

    [Theory]
    [InlineData(40, "kg", 1)]
    [InlineData(40, "KG", 1)]
    [InlineData(40, null, 1)]        // sin unidad se asume kg, como en el reporte viejo
    [InlineData(3, "bultos", 3)]     // ya viene en bultos: no se convierte
    [InlineData(3, "Bulto", 3)]
    public void ABultos_MismoCriterioQueElReporteViejo(decimal cantidad, string? unidad, decimal esperado)
    {
        Assert.Equal(esperado, ReporteAlimentoInventarioCalculos.ABultos(cantidad, unidad, 40m));
    }

    [Fact]
    public void ABultos_FactorCero_NoRevienta()
    {
        Assert.Equal(0m, ReporteAlimentoInventarioCalculos.ABultos(100m, "kg", 0m));
    }
}
