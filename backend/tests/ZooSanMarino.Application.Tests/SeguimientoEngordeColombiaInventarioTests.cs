using System.Text.Json;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Sub-fase 2b — el seguimiento de aves de engorde Colombia ahora descuenta del inventario
/// MODELO B a NIVEL GRANJA al crear/editar/eliminar, igual que levante (antes solo lo hacían
/// Ecuador/Panamá; Colombia dependía del <c>postExit</c> modelo A del front, ya eliminado).
///
/// El servicio compone dos piezas PURAS que estos tests fijan (sin tocar EF):
///   (1) <see cref="InventarioConsumoGate.ResolverModelo"/> enruta el lote Colombia a
///       <see cref="ModeloInventarioConsumo.ModeloBNivelGranja"/> → path IColombiaInventarioConsumoService.
///   (2) <see cref="MetadataEngordeCalculos.ParseMetadataItemsToKg"/> obtiene los kg por
///       catalogItemId (los ítems Colombia traen SOLO catalogItemId; el id-mapping A→B lo hace el
///       servicio). Es exactamente el diccionario que Validar/AplicarConsumo/AplicarDiff/AplicarDevolución
///       reciben en las ramas Colombia de Create/Update/Delete.
///
/// La aritmética de create (positivos), update (diff→incrementos/devoluciones) y delete (devolución
/// total) replicada aquí es la MISMA que las ramas Colombia de SeguimientoAvesEngordeService /
/// SeguimientoAvesEngordeEcuadorService. La mutación real del stock (RegistrarConsumoNivelGranjaAsync)
/// y el id-mapping A→B se prueban en ColombiaInventarioIdResolutionCalculosTests.
/// </summary>
public class SeguimientoEngordeColombiaInventarioTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    // ── Enrutamiento por país ──────────────────────────────────────────────────────────────
    [Fact]
    public void ColombiaEngorde_EnrutaAModeloBNivelGranja()
        => Assert.Equal(
            ModeloInventarioConsumo.ModeloBNivelGranja,
            InventarioConsumoGate.ResolverModelo(InventarioConsumoGate.PaisColombia));

    [Theory]
    [InlineData(2)] // Ecuador
    [InlineData(3)] // Panamá
    public void EcuadorPanamaEngorde_SiguenEnModeloBConGalpon_NoPathColombia(int pais)
    {
        // No hay doble descuento: EC/PA permanecen en el modelo B "con galpón" (RegistrarConsumoAsync),
        // NO en el path Colombia (nivel granja). El gate booleano lo confirma coherente.
        Assert.Equal(ModeloInventarioConsumo.ModeloB, InventarioConsumoGate.ResolverModelo(pais));
        Assert.NotEqual(ModeloInventarioConsumo.ModeloBNivelGranja, InventarioConsumoGate.ResolverModelo(pais));
        Assert.True(InventarioConsumoGate.DebeDescontarModeloB(pais));
    }

    // ── CREATE: consume TODO ítem positivo del metadata (H + M + generales), por catalogItemId ──
    [Fact]
    public void ColombiaEngorde_Create_PositivosPorCatalogItemId_DesdeMetadata()
    {
        // Ítems Colombia: solo catalogItemId (sin itemInventarioEcuadorId → fallback a catalogItemId).
        var metadata = Parse(@"{
            ""itemsHembras"":   [ { ""catalogItemId"": 89, ""cantidad"": 40, ""unidad"": ""kg"" } ],
            ""itemsMachos"":    [ { ""catalogItemId"": 89, ""cantidad"": 10, ""unidad"": ""kg"" } ],
            ""itemsGenerales"": [ { ""catalogItemId"": 200, ""cantidad"": 2000, ""unidad"": ""g"" } ]
        }");
        var byItem = MetadataEngordeCalculos.ParseMetadataItemsToKg(metadata);

        // positivos = lo que la rama Colombia de CreateAsync pasa a ValidarStock/AplicarConsumo.
        var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Equal(50m, positivos[89]);   // 40 (H) + 10 (M) → mismo ítem se suma
        Assert.Equal(2m, positivos[200]);   // 2000 g → 2 kg (itemsGenerales aditivo)
        Assert.Equal(2, positivos.Count);
    }

    [Fact]
    public void ColombiaEngorde_Create_SinItems_NoConsumeNada()
    {
        var metadata = Parse(@"{ ""observaciones"": ""solo peso, sin alimento"" }");
        var byItem = MetadataEngordeCalculos.ParseMetadataItemsToKg(metadata);
        var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Empty(positivos);
    }

    // ── UPDATE: diff old/new por catalogItemId → incrementos (>0) a validar/consumir; <0 = devolución ──
    [Fact]
    public void ColombiaEngorde_Update_Diff_IncrementosSoloPositivos()
    {
        var oldMeta = Parse(@"{ ""itemsHembras"": [ { ""catalogItemId"": 89, ""cantidad"": 30, ""unidad"": ""kg"" } ] }");
        var newMeta = Parse(@"{
            ""itemsHembras"": [ { ""catalogItemId"": 89, ""cantidad"": 50, ""unidad"": ""kg"" } ],
            ""itemsMachos"":  [ { ""catalogItemId"": 90, ""cantidad"": 5,  ""unidad"": ""kg"" } ]
        }");
        var (incrementos, devoluciones) = DiffIncrementosDevoluciones(oldMeta, newMeta);

        Assert.Equal(20m, incrementos[89]);   // 50 - 30 = consumo adicional
        Assert.Equal(5m, incrementos[90]);    // ítem nuevo
        Assert.Equal(2, incrementos.Count);
        Assert.Empty(devoluciones);
    }

    [Fact]
    public void ColombiaEngorde_Update_MenorConsumo_GeneraDevolucion_SinIncrementoAValidar()
    {
        var oldMeta = Parse(@"{ ""itemsHembras"": [ { ""catalogItemId"": 89, ""cantidad"": 50, ""unidad"": ""kg"" } ] }");
        var newMeta = Parse(@"{ ""itemsHembras"": [ { ""catalogItemId"": 89, ""cantidad"": 20, ""unidad"": ""kg"" } ] }");
        var (incrementos, devoluciones) = DiffIncrementosDevoluciones(oldMeta, newMeta);

        Assert.Empty(incrementos);            // no hay nada que validar/consumir de más
        Assert.Equal(30m, devoluciones[89]);  // se reponen 30 kg (AplicarDiffAsync → ingreso)
    }

    // ── DELETE: devolución total de los ítems positivos del metadata ───────────────────────────
    [Fact]
    public void ColombiaEngorde_Delete_DevuelveTodoItemPositivo()
    {
        var metadata = Parse(@"{
            ""itemsHembras"":   [ { ""catalogItemId"": 89, ""cantidad"": 40, ""unidad"": ""kg"" } ],
            ""itemsGenerales"": [ { ""catalogItemId"": 200, ""cantidad"": 1, ""unidad"": ""kg"" } ]
        }");
        var byItem = MetadataEngordeCalculos.ParseMetadataItemsToKg(metadata);
        var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Equal(40m, positivos[89]);
        Assert.Equal(1m, positivos[200]);
        Assert.Equal(2, positivos.Count);
    }

    /// <summary>
    /// Réplica exacta de la aritmética de diff de la rama Colombia de UpdateAsync:
    /// incrementos = diffs &gt; 0 (se validan/consumen); devoluciones = |diffs &lt; 0| (se reponen).
    /// </summary>
    private static (Dictionary<int, decimal> Incrementos, Dictionary<int, decimal> Devoluciones)
        DiffIncrementosDevoluciones(JsonElement oldMeta, JsonElement newMeta)
    {
        var oldById = MetadataEngordeCalculos.ParseMetadataItemsToKg(oldMeta);
        var newById = MetadataEngordeCalculos.ParseMetadataItemsToKg(newMeta);
        var incrementos = new Dictionary<int, decimal>();
        var devoluciones = new Dictionary<int, decimal>();
        var all = new HashSet<int>(oldById.Keys);
        foreach (var k in newById.Keys) all.Add(k);
        foreach (var id in all)
        {
            var diff = newById.GetValueOrDefault(id) - oldById.GetValueOrDefault(id);
            if (diff > 0) incrementos[id] = diff;
            else if (diff < 0) devoluciones[id] = -diff;
        }
        return (incrementos, devoluciones);
    }
}
