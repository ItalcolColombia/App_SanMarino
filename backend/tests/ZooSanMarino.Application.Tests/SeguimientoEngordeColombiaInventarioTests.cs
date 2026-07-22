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
///   (2) <see cref="MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen"/> obtiene los kg por
///       CLAVE TIPADA (fix multi-empresa jul-2026: el origen del id viaja explícito — camino 1 =
///       catalogItemId, camino 2 = itemInventarioEcuadorId — y el id-mapping A→B lo hace el
///       servicio contra la empresa efectiva de la granja). Es exactamente el diccionario que
///       Validar/AplicarConsumo/AplicarDiff/AplicarDevolución reciben en las ramas Colombia de
///       Create/Update/Delete.
///
/// La aritmética de create (positivos), update (diff→incrementos/devoluciones) y delete (devolución
/// total) replicada aquí es la MISMA que las ramas Colombia de SeguimientoAvesEngordeService /
/// SeguimientoAvesEngordeEcuadorService. La mutación real del stock (RegistrarConsumoNivelGranjaAsync)
/// y el id-mapping A→B se prueban en ColombiaInventarioIdResolutionCalculosTests.
/// </summary>
public class SeguimientoEngordeColombiaInventarioTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;
    private static ItemConsumoKey Catalogo(int id) => new(id, EsItemInventario: false);
    private static ItemConsumoKey Inventario(int id) => new(id, EsItemInventario: true);

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

    // ── CREATE: consume TODO ítem positivo del metadata (H + M + generales), por clave tipada ──
    [Fact]
    public void ColombiaEngorde_Create_PositivosPorClaveTipada_DesdeMetadata()
    {
        // Ítems Colombia con espejo en catálogo: solo catalogItemId (camino 1).
        var metadata = Parse(@"{
            ""itemsHembras"":   [ { ""catalogItemId"": 89, ""cantidad"": 40, ""unidad"": ""kg"" } ],
            ""itemsMachos"":    [ { ""catalogItemId"": 89, ""cantidad"": 10, ""unidad"": ""kg"" } ],
            ""itemsGenerales"": [ { ""catalogItemId"": 200, ""cantidad"": 2000, ""unidad"": ""g"" } ]
        }");
        var byItem = MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(metadata);

        // positivos = lo que la rama Colombia de CreateAsync pasa a ValidarStock/AplicarConsumo.
        var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Equal(50m, positivos[Catalogo(89)]);   // 40 (H) + 10 (M) → mismo ítem se suma
        Assert.Equal(2m, positivos[Catalogo(200)]);   // 2000 g → 2 kg (itemsGenerales aditivo)
        Assert.Equal(2, positivos.Count);
    }

    [Fact]
    public void ColombiaEngorde_Create_ItemInventarioNuevoSinEspejo_ViajaComoCamino2()
    {
        // Caso Demo ("Alimneto ERP" id 208): catalogItemId=0 + itemInventarioEcuadorId → la clave
        // conserva el camino 2 y el servicio lo valida contra la empresa efectiva de la granja
        // (antes se re-interpretaba como catalogItemId y el 400 "no tiene equivalente" era inevitable).
        var metadata = Parse(@"{
            ""itemsHembras"": [ { ""catalogItemId"": 0, ""itemInventarioEcuadorId"": 208, ""cantidad"": 400, ""unidad"": ""kg"" } ]
        }");
        var byItem = MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(metadata);
        var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

        var entry = Assert.Single(positivos);
        Assert.Equal(Inventario(208), entry.Key);
        Assert.Equal(400m, entry.Value);
    }

    [Fact]
    public void ColombiaEngorde_Create_SinItems_NoConsumeNada()
    {
        var metadata = Parse(@"{ ""observaciones"": ""solo peso, sin alimento"" }");
        var byItem = MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(metadata);
        var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Empty(positivos);
    }

    // ── UPDATE: diff old/new por clave tipada → incrementos (>0) a validar/consumir; <0 = devolución ──
    [Fact]
    public void ColombiaEngorde_Update_Diff_IncrementosSoloPositivos()
    {
        var oldMeta = Parse(@"{ ""itemsHembras"": [ { ""catalogItemId"": 89, ""cantidad"": 30, ""unidad"": ""kg"" } ] }");
        var newMeta = Parse(@"{
            ""itemsHembras"": [ { ""catalogItemId"": 89, ""cantidad"": 50, ""unidad"": ""kg"" } ],
            ""itemsMachos"":  [ { ""catalogItemId"": 90, ""cantidad"": 5,  ""unidad"": ""kg"" } ]
        }");
        var (incrementos, devoluciones) = DiffIncrementosDevoluciones(oldMeta, newMeta);

        Assert.Equal(20m, incrementos[Catalogo(89)]);   // 50 - 30 = consumo adicional
        Assert.Equal(5m, incrementos[Catalogo(90)]);    // ítem nuevo
        Assert.Equal(2, incrementos.Count);
        Assert.Empty(devoluciones);
    }

    [Fact]
    public void ColombiaEngorde_Update_MenorConsumo_GeneraDevolucion_SinIncrementoAValidar()
    {
        var oldMeta = Parse(@"{ ""itemsHembras"": [ { ""catalogItemId"": 89, ""cantidad"": 50, ""unidad"": ""kg"" } ] }");
        var newMeta = Parse(@"{ ""itemsHembras"": [ { ""catalogItemId"": 89, ""cantidad"": 20, ""unidad"": ""kg"" } ] }");
        var (incrementos, devoluciones) = DiffIncrementosDevoluciones(oldMeta, newMeta);

        Assert.Empty(incrementos);                     // no hay nada que validar/consumir de más
        Assert.Equal(30m, devoluciones[Catalogo(89)]); // se reponen 30 kg (AplicarDiffAsync → ingreso)
    }

    [Fact]
    public void ColombiaEngorde_Update_CambioDeEspejoAItemNuevo_DiffPorClave_NoPorNumero()
    {
        // Registro viejo con catalogItemId=89 editado hacia un ítem del inventario nuevo cuyo id
        // numérico TAMBIÉN es 89 (colisión): son claves distintas → devolución del catálogo 89 e
        // incremento del inventario 89 (antes se anulaban entre sí en silencio).
        var oldMeta = Parse(@"{ ""itemsHembras"": [ { ""catalogItemId"": 89, ""cantidad"": 30, ""unidad"": ""kg"" } ] }");
        var newMeta = Parse(@"{ ""itemsHembras"": [ { ""catalogItemId"": 0, ""itemInventarioEcuadorId"": 89, ""cantidad"": 30, ""unidad"": ""kg"" } ] }");
        var (incrementos, devoluciones) = DiffIncrementosDevoluciones(oldMeta, newMeta);

        Assert.Equal(30m, incrementos[Inventario(89)]);
        Assert.Equal(30m, devoluciones[Catalogo(89)]);
    }

    // ── DELETE: devolución total de los ítems positivos del metadata ───────────────────────────
    [Fact]
    public void ColombiaEngorde_Delete_DevuelveTodoItemPositivo()
    {
        var metadata = Parse(@"{
            ""itemsHembras"":   [ { ""catalogItemId"": 89, ""cantidad"": 40, ""unidad"": ""kg"" } ],
            ""itemsGenerales"": [ { ""catalogItemId"": 200, ""cantidad"": 1, ""unidad"": ""kg"" } ]
        }");
        var byItem = MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(metadata);
        var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Equal(40m, positivos[Catalogo(89)]);
        Assert.Equal(1m, positivos[Catalogo(200)]);
        Assert.Equal(2, positivos.Count);
    }

    /// <summary>
    /// Réplica exacta de la aritmética de diff de la rama Colombia de UpdateAsync:
    /// incrementos = diffs &gt; 0 (se validan/consumen); devoluciones = |diffs &lt; 0| (se reponen).
    /// </summary>
    private static (Dictionary<ItemConsumoKey, decimal> Incrementos, Dictionary<ItemConsumoKey, decimal> Devoluciones)
        DiffIncrementosDevoluciones(JsonElement oldMeta, JsonElement newMeta)
    {
        var oldByKey = MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(oldMeta);
        var newByKey = MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(newMeta);
        var incrementos = new Dictionary<ItemConsumoKey, decimal>();
        var devoluciones = new Dictionary<ItemConsumoKey, decimal>();
        var all = new HashSet<ItemConsumoKey>(oldByKey.Keys);
        foreach (var k in newByKey.Keys) all.Add(k);
        foreach (var key in all)
        {
            var diff = newByKey.GetValueOrDefault(key) - oldByKey.GetValueOrDefault(key);
            if (diff > 0) incrementos[key] = diff;
            else if (diff < 0) devoluciones[key] = -diff;
        }
        return (incrementos, devoluciones);
    }
}
