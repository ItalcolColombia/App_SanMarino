using System.Text.Json;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de MetadataEngordeCalculos.ParseMetadataItemsToKg.
/// Fase 2 (S2b): el parser se extendió a itemsGenerales (aditivo) para que Colombia
/// descuente "todos los ítems". Se preserva el comportamiento Ecuador (fallback
/// itemInventarioEcuadorId → catalogItemId; itemsHembras + itemsMachos).
/// </summary>
public class MetadataEngordeCalculosTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void ToKg_GramosDivideEntre1000_KgSeMantiene()
    {
        Assert.Equal(0.5m, MetadataEngordeCalculos.ToKg(500, "g"));
        Assert.Equal(0.5m, MetadataEngordeCalculos.ToKg(500, "gramos"));
        Assert.Equal(2m, MetadataEngordeCalculos.ToKg(2, "kg"));
        Assert.Equal(2m, MetadataEngordeCalculos.ToKg(2, null));  // default kg
    }

    [Fact]
    public void HembrasYMachos_SeAcumulan_ComportamientoEcuadorPreservado()
    {
        // Ecuador usa itemInventarioEcuadorId; itemsHembras + itemsMachos.
        var root = Parse(@"{
            ""itemsHembras"": [ { ""itemInventarioEcuadorId"": 10, ""cantidad"": 5, ""unidad"": ""kg"" } ],
            ""itemsMachos"":  [ { ""itemInventarioEcuadorId"": 10, ""cantidad"": 3, ""unidad"": ""kg"" },
                                { ""itemInventarioEcuadorId"": 20, ""cantidad"": 2000, ""unidad"": ""g"" } ]
        }");
        var r = MetadataEngordeCalculos.ParseMetadataItemsToKg(root);
        Assert.Equal(8m, r[10]);   // 5 (H) + 3 (M)
        Assert.Equal(2m, r[20]);   // 2000 g => 2 kg
        Assert.Equal(2, r.Count);
    }

    [Fact]
    public void FallbackCatalogItemId_CuandoNoHayItemInventarioEcuadorId()
    {
        // Colombia: los ítems traen SOLO catalogItemId (sin itemInventarioEcuadorId).
        var root = Parse(@"{
            ""itemsHembras"": [ { ""catalogItemId"": 89, ""cantidad"": 40, ""unidad"": ""kg"" } ]
        }");
        var r = MetadataEngordeCalculos.ParseMetadataItemsToKg(root);
        Assert.Equal(40m, r[89]);
    }

    [Fact]
    public void ItemsGenerales_SeAcumulan_Fase2()
    {
        // Fase 2: itemsGenerales (medicamentos/insumos que no van por H/M) también descuentan.
        var root = Parse(@"{
            ""itemsHembras"":   [ { ""catalogItemId"": 100, ""cantidad"": 10, ""unidad"": ""kg"" } ],
            ""itemsGenerales"": [ { ""catalogItemId"": 200, ""cantidad"": 1, ""unidad"": ""kg"" },
                                  { ""catalogItemId"": 100, ""cantidad"": 5, ""unidad"": ""kg"" } ]
        }");
        var r = MetadataEngordeCalculos.ParseMetadataItemsToKg(root);
        Assert.Equal(15m, r[100]);  // 10 (H) + 5 (Generales) → mismo ítem se suma
        Assert.Equal(1m, r[200]);   // solo en generales
        Assert.Equal(2, r.Count);
    }

    [Fact]
    public void SinItemsGenerales_ResultadoIdenticoAlPrevio()
    {
        // Guarda de no-regresión: un metadata sin itemsGenerales (caso Ecuador típico)
        // produce EXACTAMENTE lo mismo que antes de la extensión.
        var root = Parse(@"{
            ""itemsHembras"": [ { ""itemInventarioEcuadorId"": 7, ""cantidad"": 12.5, ""unidad"": ""kg"" } ]
        }");
        var r = MetadataEngordeCalculos.ParseMetadataItemsToKg(root);
        Assert.Single(r);
        Assert.Equal(12.5m, r[7]);
    }

    [Fact]
    public void PropiedadesNoArray_SeIgnoran_GuardaDefensiva()
    {
        var root = Parse(@"{ ""itemsHembras"": 123, ""itemsGenerales"": ""x"" }");
        var r = MetadataEngordeCalculos.ParseMetadataItemsToKg(root);
        Assert.Empty(r);
    }
}
