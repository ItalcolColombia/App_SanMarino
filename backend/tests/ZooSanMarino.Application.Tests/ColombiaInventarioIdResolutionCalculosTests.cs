using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato TIPADO del id-mapping Colombia (fix multi-empresa jul-2026): el origen del id viaja
/// explícito en <see cref="ItemConsumoKey"/> (camino 1 = catalogItemId por código; camino 2 =
/// itemInventarioEcuadorId pass-through validado contra la empresa efectiva). Antes el servicio
/// ADIVINABA la tabla por existencia en catalogo_items y las colisiones numéricas entre ambas
/// tablas (rangos solapados) producían el 400 "no tiene equivalente" o riesgo de descuento cruzado.
/// </summary>
public class ColombiaInventarioIdResolutionCalculosTests
{
    private static ItemConsumoKey Catalogo(int id) => new(id, EsItemInventario: false);
    private static ItemConsumoKey Inventario(int id) => new(id, EsItemInventario: true);

    [Fact]
    public void Camino1_ResuelvePorCodigo_CuandoElCatalogItemTieneEquivalenteEnItemInventario()
    {
        var map = ColombiaInventarioIdResolutionCalculos.Resolver(
            catalogItemsEncontrados: new[] { (Id: 5, Codigo: "COD1") },
            itemsBPorCodigoEncontrados: new[] { (Id: 147, Codigo: "COD1") },
            itemsBDirectosValidos: Array.Empty<int>());

        var entry = Assert.Single(map);
        Assert.Equal(Catalogo(5), entry.Key);
        Assert.Equal(147, entry.Value);
    }

    [Fact]
    public void Camino2_PassThrough_DeUnItemInventarioValidoDeLaEmpresaEfectiva()
    {
        // Caso Demo: ítem 208 "Alimneto ERP" creado en el inventario nuevo por la empresa de la
        // granja (sin fila espejo en catalogo_items). itemsBDirectosValidos ya viene filtrado por
        // empresa efectiva + país Colombia → se acepta tal cual.
        var map = ColombiaInventarioIdResolutionCalculos.Resolver(
            catalogItemsEncontrados: Array.Empty<(int Id, string Codigo)>(),
            itemsBPorCodigoEncontrados: Array.Empty<(int Id, string Codigo)>(),
            itemsBDirectosValidos: new[] { 208 });

        var entry = Assert.Single(map);
        Assert.Equal(Inventario(208), entry.Key);
        Assert.Equal(208, entry.Value);
    }

    [Fact]
    public void Camino2_NoResuelve_SiElItemNoEsValidoParaLaEmpresaEfectiva()
    {
        // El ítem existe en otra empresa (p.ej. id de Demo consultado con granja Sanmarino):
        // la query por empresa efectiva no lo devuelve → no figura → el servicio lanza.
        var map = ColombiaInventarioIdResolutionCalculos.Resolver(
            catalogItemsEncontrados: Array.Empty<(int Id, string Codigo)>(),
            itemsBPorCodigoEncontrados: Array.Empty<(int Id, string Codigo)>(),
            itemsBDirectosValidos: Array.Empty<int>());

        Assert.Empty(map);
    }

    [Fact]
    public void ColisionNumericaEntreTablas_CadaClaveResuelvePorSuPropioCamino()
    {
        // id=5 existe EN AMBAS tablas (colisión de rangos). Con el origen explícito ya no se
        // adivina: (5, catálogo) → espejo por código (147); (5, inventario) → pass-through (5).
        // Antes esta colisión forzaba el camino 1 para ambos y el ítem del inventario nuevo
        // terminaba en 400 "no tiene equivalente" (o mapeado a otro producto).
        var map = ColombiaInventarioIdResolutionCalculos.Resolver(
            catalogItemsEncontrados: new[] { (Id: 5, Codigo: "COD1") },
            itemsBPorCodigoEncontrados: new[] { (Id: 147, Codigo: "COD1") },
            itemsBDirectosValidos: new[] { 5 });

        Assert.Equal(2, map.Count);
        Assert.Equal(147, map[Catalogo(5)]);
        Assert.Equal(5, map[Inventario(5)]);
    }

    [Fact]
    public void Camino1_SinEquivalentePorCodigo_NoResuelve()
    {
        // Gap de migración A→B: el catálogo existe pero no hay ítem B con ese código para la
        // empresa efectiva → no figura → el servicio lanza (mismo criterio de siempre).
        var map = ColombiaInventarioIdResolutionCalculos.Resolver(
            catalogItemsEncontrados: new[] { (Id: 5, Codigo: "COD-SIN-MIGRAR") },
            itemsBPorCodigoEncontrados: Array.Empty<(int Id, string Codigo)>(),
            itemsBDirectosValidos: Array.Empty<int>());

        Assert.Empty(map);
    }

    [Fact]
    public void Resuelve_MezclaDeItemsHistoricosPorCodigo_YItemsNuevosPorPassThrough()
    {
        var map = ColombiaInventarioIdResolutionCalculos.Resolver(
            catalogItemsEncontrados: new[] { (Id: 5, Codigo: "COD1"), (Id: 6, Codigo: "COD2") },
            itemsBPorCodigoEncontrados: new[] { (Id: 147, Codigo: "COD1"), (Id: 148, Codigo: "COD2") },
            itemsBDirectosValidos: new[] { 300, 301 });

        Assert.Equal(4, map.Count);
        Assert.Equal(147, map[Catalogo(5)]);
        Assert.Equal(148, map[Catalogo(6)]);
        Assert.Equal(300, map[Inventario(300)]);
        Assert.Equal(301, map[Inventario(301)]);
    }

    [Fact]
    public void Codigo_SeComparaNormalizado_TrimYCaseInsensitive()
    {
        var map = ColombiaInventarioIdResolutionCalculos.Resolver(
            catalogItemsEncontrados: new[] { (Id: 5, Codigo: "  Cod1  ") },
            itemsBPorCodigoEncontrados: new[] { (Id: 147, Codigo: "cod1") },
            itemsBDirectosValidos: Array.Empty<int>());

        Assert.Equal(147, map[Catalogo(5)]);
    }
}
