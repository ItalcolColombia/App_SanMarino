using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.PaginacionCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Normalización del tamaño de página. El caso que blinda esta clase es
/// <see cref="PedirDeMasDevuelveElTope_NoElDefault"/>: el clamp viejo
/// (<c>pageSize &gt; 200 ⇒ 20</c>) convertía un pedido excesivo en el MÍNIMO, y esa degradación
/// silenciosa costó dos incidentes (Reporte Contable viendo 20 movimientos por granja; siete
/// pantallas del front recibiendo 20 ítems de catálogo tras pedir 1.000-2.000).
/// </summary>
public class PaginacionCalculosTests
{
    [Theory]
    [InlineData(10000, MaximoListadoTransaccional, MaximoListadoTransaccional)] // Reporte Contable
    [InlineData(201,   MaximoListadoTransaccional, MaximoListadoTransaccional)] // apenas pasado el tope
    [InlineData(5000,  MaximoCatalogoMaestro,      MaximoCatalogoMaestro)]      // catálogo, pedido absurdo
    public void PedirDeMasDevuelveElTope_NoElDefault(int pedido, int maximo, int esperado)
    {
        var size = NormalizarPageSize(pedido, maximo);

        Assert.Equal(esperado, size);
        // Lo que NUNCA puede volver a pasar: pedir de más y recibir el mínimo.
        Assert.NotEqual(PageSizePorDefecto, size);
        Assert.True(size > PageSizePorDefecto);
    }

    [Theory]
    // Lo que piden REALMENTE las pantallas del front al catálogo: con el tope maestro ya no se
    // recortan, así que reciben el catálogo entero (el más grande en producción tiene 310 ítems).
    [InlineData(1000)]  // inventario.service.ts getCatalogo()
    [InlineData(2000)]  // modales de seguimiento de levante y producción
    public void LasPantallasDelFrontRecibenLoQuePiden_YaNo20(int pedidoDelFront)
    {
        var size = NormalizarPageSize(pedidoDelFront, MaximoCatalogoMaestro);

        Assert.Equal(pedidoDelFront, size);
        Assert.True(size >= 310, "debe cubrir el catálogo más grande de producción");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NoEspecificarNadaDevuelveElDefault(int pedido)
    {
        Assert.Equal(PageSizePorDefecto, NormalizarPageSize(pedido));
    }

    [Fact]
    public void ElDefaultEsConfigurable_ParaConservarElDeCadaListado()
    {
        // RoleCompositeService venía con default 50: se conserva.
        Assert.Equal(50, NormalizarPageSize(0, MaximoListadoTransaccional, porDefecto: 50));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(200)]
    public void DentroDeRangoPasaIgual(int pedido)
    {
        Assert.Equal(pedido, NormalizarPageSize(pedido));
    }

    [Fact]
    public void ElTopeExactoNoSeRecorta()
    {
        Assert.Equal(MaximoCatalogoMaestro, NormalizarPageSize(MaximoCatalogoMaestro, MaximoCatalogoMaestro));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void NormalizarPage_NuncaDevuelveMenorQueUno(int pedido, int esperado)
    {
        Assert.Equal(esperado, NormalizarPage(pedido));
    }

    [Fact]
    public void ElTopeDelCatalogoCubreElCatalogoMasGrandeConMargen()
    {
        // Santa Reyes (company 6) es el catálogo más grande en producción: 310 ítems.
        const int catalogoMasGrande = 310;
        Assert.True(MaximoCatalogoMaestro >= catalogoMasGrande * 6);
        Assert.Equal(catalogoMasGrande, NormalizarPageSize(catalogoMasGrande, MaximoCatalogoMaestro));
    }
}
