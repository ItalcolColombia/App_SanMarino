using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

public class ColombiaInventarioIdResolutionCalculosTests
{
    [Fact]
    public void Resuelve_por_codigo_cuando_el_catalogItem_tiene_equivalente_en_item_inventario_ecuador()
    {
        var map = ColombiaInventarioIdResolutionCalculos.Resolver(
            catalogItemsEncontrados: new[] { (Id: 5, Codigo: "COD1") },
            itemsBPorCodigoEncontrados: new[] { (Id: 147, Codigo: "COD1") },
            itemsBDirectosValidos: Array.Empty<int>());

        var itemBId = Assert.Single(map);
        Assert.Equal(5, itemBId.Key);
        Assert.Equal(147, itemBId.Value);
    }

    [Fact]
    public void Pass_through_cuando_el_id_nunca_existio_en_catalogo_items_pero_es_un_item_inventario_ecuador_valido()
    {
        // Ítem creado directamente en el inventario nuevo (Config > Ítems de inventario), sin fila espejo en catalogo_items.
        var map = ColombiaInventarioIdResolutionCalculos.Resolver(
            catalogItemsEncontrados: Array.Empty<(int Id, string Codigo)>(),
            itemsBPorCodigoEncontrados: Array.Empty<(int Id, string Codigo)>(),
            itemsBDirectosValidos: new[] { 300 });

        var itemBId = Assert.Single(map);
        Assert.Equal(300, itemBId.Key);
        Assert.Equal(300, itemBId.Value);
    }

    [Fact]
    public void No_resuelve_por_pass_through_un_id_que_existe_en_catalogo_items_aunque_coincida_numericamente_con_un_item_B_valido()
    {
        // id=5 SÍ existe en catalogo_items pero su código no tiene equivalente en item_inventario_ecuador
        // (gap de migración). Aunque el número 5 TAMBIÉN sea, por coincidencia, un item_inventario_ecuador.id
        // válido (de otro ítem sin relación), NO debe resolverse por el camino 2 — evita descontar el ítem
        // equivocado por colisión de ids entre las dos tablas.
        var map = ColombiaInventarioIdResolutionCalculos.Resolver(
            catalogItemsEncontrados: new[] { (Id: 5, Codigo: "COD-SIN-MIGRAR") },
            itemsBPorCodigoEncontrados: Array.Empty<(int Id, string Codigo)>(),
            itemsBDirectosValidos: new[] { 5 });

        Assert.Empty(map);
    }

    [Fact]
    public void Resuelve_mezcla_de_items_historicos_por_codigo_y_items_nuevos_por_pass_through()
    {
        var map = ColombiaInventarioIdResolutionCalculos.Resolver(
            catalogItemsEncontrados: new[] { (Id: 5, Codigo: "COD1"), (Id: 6, Codigo: "COD2") },
            itemsBPorCodigoEncontrados: new[] { (Id: 147, Codigo: "COD1"), (Id: 148, Codigo: "COD2") },
            itemsBDirectosValidos: new[] { 300, 301 });

        Assert.Equal(4, map.Count);
        Assert.Equal(147, map[5]);
        Assert.Equal(148, map[6]);
        Assert.Equal(300, map[300]);
        Assert.Equal(301, map[301]);
    }

    [Fact]
    public void Codigo_se_compara_normalizado_trim_y_case_insensitive()
    {
        var map = ColombiaInventarioIdResolutionCalculos.Resolver(
            catalogItemsEncontrados: new[] { (Id: 5, Codigo: "  Cod1  ") },
            itemsBPorCodigoEncontrados: new[] { (Id: 147, Codigo: "cod1") },
            itemsBDirectosValidos: Array.Empty<int>());

        Assert.Equal(147, map[5]);
    }
}
