using System.Text.Json;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// F1 del plan `descuento_inventario_movil_plan.md` — contrato de
/// <see cref="ItemConsumoCalculos.AcumularPorOrigen"/>, el acumulador que decide QUÉ y CUÁNTO se
/// descuenta del inventario cuando se guarda un seguimiento diario de producción.
///
/// <para>
/// <b>Por qué estos tests importan más de lo que parece.</b> Equivocarse acá no tira una excepción:
/// descuenta el ítem equivocado, o el silo equivocado, o cuenta los kilos dos veces, y se descubre
/// días después cuando al operario no le cuadra el saldo. Hasta F1 este código vivía dentro de
/// <c>Infrastructure/Services/ProduccionService.cs</c> y el proyecto de tests no referencia
/// Infrastructure: no había forma de cubrirlo.
/// </para>
///
/// <para>
/// La segunda mitad del archivo fija la <b>equivalencia acotada</b> con
/// <see cref="MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen"/>, que es la ruta que lee los
/// mismos ítems desde el metadata jsonb al editar o borrar. Las dos rutas tienen que dar el mismo
/// número para el mismo día; si divergen, editar un seguimiento devolvería al stock una cantidad
/// distinta de la que se descontó al crearlo.
/// </para>
/// </summary>
public class ItemConsumoCalculosTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static ItemSeguimientoDto Item(
        int catalogItemId = 0,
        int? itemInventarioEcuadorId = null,
        double cantidad = 0,
        string unidad = "kg",
        int? siloId = null,
        string tipoItem = "alimento",
        string? nombre = null) =>
        new()
        {
            TipoItem = tipoItem,
            CatalogItemId = catalogItemId,
            ItemInventarioEcuadorId = itemInventarioEcuadorId,
            Nombre = nombre,
            Cantidad = cantidad,
            Unidad = unidad,
            SiloId = siloId
        };

    private static ItemConsumoKey Catalogo(int id, int? silo = null) => new(id, EsItemInventario: false, SiloId: silo);
    private static ItemConsumoKey Inventario(int id, int? silo = null) => new(id, EsItemInventario: true, SiloId: silo);

    /// <summary>
    /// Construye el JSON de <c>metadata</c> que consume <see cref="MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen"/>,
    /// para comparar el camino-metadata contra <see cref="ItemConsumoCalculos.AcumularPorOrigen"/>
    /// (camino-request) sobre datos equivalentes.
    ///
    /// Incluye <c>"nombre"</c> porque así lo persiste <c>ItemAMetadata</c> de
    /// <c>CreateSeguimientoLoteLevanteRequest.cs</c> — el de levante/engorde, NO el privado homónimo
    /// de <c>ProduccionService.cs</c>, que omite esa clave. La diferencia es irrelevante para este
    /// test: el parser sólo lee <c>itemInventarioEcuadorId</c>/<c>catalogItemId</c>/<c>cantidad</c>/
    /// <c>unidad</c>/<c>siloId</c> y descarta cualquier otra clave, así que ninguna de las dos formas
    /// reales cambia el resultado.
    /// </summary>
    private static JsonElement AMetadata(
        IEnumerable<ItemSeguimientoDto>? itemsHembras,
        IEnumerable<ItemSeguimientoDto>? itemsMachos,
        IEnumerable<ItemSeguimientoDto>? itemsGenerales = null)
    {
        static List<Dictionary<string, object?>> Bloque(IEnumerable<ItemSeguimientoDto> items) =>
            items.Select(i =>
            {
                var item = new Dictionary<string, object?>
                {
                    ["tipoItem"] = i.TipoItem,
                    ["catalogItemId"] = i.CatalogItemId,
                    ["itemInventarioEcuadorId"] = i.ItemInventarioEcuadorId,
                    ["nombre"] = i.Nombre,
                    ["cantidad"] = i.Cantidad,
                    ["unidad"] = i.Unidad
                };
                if (i.SiloId is > 0) item["siloId"] = i.SiloId.Value;
                return item;
            }).ToList();

        var metadata = new Dictionary<string, object?>();
        if (itemsHembras != null) metadata["itemsHembras"] = Bloque(itemsHembras);
        if (itemsMachos != null) metadata["itemsMachos"] = Bloque(itemsMachos);
        if (itemsGenerales != null) metadata["itemsGenerales"] = Bloque(itemsGenerales);
        return JsonDocument.Parse(JsonSerializer.Serialize(metadata)).RootElement;
    }

    // ── Prioridad del id y marca de origen ──────────────────────────────────────────────────

    /// <summary>
    /// Sin id de inventario, el ítem es del catálogo legacy. La marca en <c>false</c> es lo que evita
    /// que en Colombia —donde los dos rangos de id conviven y colisionan— el mismo número se resuelva
    /// contra la tabla equivocada.
    /// </summary>
    [Fact]
    public void SoloCatalogItemId_LaClaveQuedaMarcadaComoCatalogo()
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[] { Item(catalogItemId: 89, cantidad: 40) }, null);

        var entry = Assert.Single(r);
        Assert.Equal(Catalogo(89), entry.Key);
        Assert.Equal(40m, entry.Value);
    }

    /// <summary>Con id del inventario unificado (camino 2) manda ese id y la clave queda marcada.</summary>
    [Fact]
    public void SoloItemInventarioEcuadorId_LaClaveQuedaMarcadaComoInventario()
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[] { Item(itemInventarioEcuadorId: 208, cantidad: 320) }, null);

        var entry = Assert.Single(r);
        Assert.Equal(Inventario(208), entry.Key);
        Assert.Equal(320m, entry.Value);
    }

    /// <summary>
    /// Cuando el front manda los dos ids, <b>gana el de inventario</b> y el de catálogo se descarta
    /// entero. Es el caso normal en Ecuador/Panamá, donde el formulario rellena ambos campos.
    /// </summary>
    [Fact]
    public void ConLosDosIds_GanaElDeInventarioYElDeCatalogoSeIgnora()
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[] { Item(catalogItemId: 89, itemInventarioEcuadorId: 208, cantidad: 10) }, null);

        var entry = Assert.Single(r);
        Assert.Equal(Inventario(208), entry.Key);
        Assert.False(r.ContainsKey(Catalogo(89)));
    }

    /// <summary>
    /// El mismo número de id con origen distinto son DOS claves. Sin esta separación, un catálogo 150
    /// y un ítem de inventario 150 se descontarían del mismo saldo.
    /// </summary>
    [Fact]
    public void MismoNumeroDeIdConOrigenDistinto_SonDosClaves()
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[] { Item(catalogItemId: 150, cantidad: 40) },
            new[] { Item(itemInventarioEcuadorId: 150, cantidad: 10) });

        Assert.Equal(2, r.Count);
        Assert.Equal(40m, r[Catalogo(150)]);
        Assert.Equal(10m, r[Inventario(150)]);
    }

    // ── Ítems sin id: se descartan en silencio ──────────────────────────────────────────────

    /// <summary>
    /// Un ítem con id 0 es la fila vacía que el formulario deja abierta por defecto y el usuario nunca
    /// completó. Se descarta <b>sin excepción</b>: reventar acá haría fallar el guardado de un día
    /// perfectamente válido por una fila que el usuario ni miró. La cantidad tipeada se pierde a
    /// propósito — sin ítem seleccionado no hay stock del que descontarla.
    /// </summary>
    [Fact]
    public void ItemConIdCero_SeDescartaEnSilencioAunqueTraigaCantidad()
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[] { Item(catalogItemId: 0, cantidad: 999) }, null);

        Assert.Empty(r);
    }

    /// <summary>
    /// Id negativo (basura de un form a medio serializar) se trata igual que el cero, y además NO
    /// marca la clave como de inventario: <c>esItemInventario</c> exige estrictamente <c>&gt; 0</c>.
    /// </summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    public void ItemSinIdUtilizableEnNingunoDeLosDosCampos_SeDescarta(int inventarioId, int catalogoId)
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[] { Item(catalogItemId: catalogoId, itemInventarioEcuadorId: inventarioId, cantidad: 50) }, null);

        Assert.Empty(r);
    }

    /// <summary>
    /// Id de inventario negativo pero catálogo válido: cae al catálogo y la marca queda en
    /// <c>false</c>. Documenta que el fallback mira <c>id &lt;= 0</c>, no <c>id == 0</c>.
    /// </summary>
    [Fact]
    public void InventarioNegativoConCatalogoValido_CaeAlCatalogo()
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[] { Item(catalogItemId: 89, itemInventarioEcuadorId: -5, cantidad: 7) }, null);

        var entry = Assert.Single(r);
        Assert.Equal(Catalogo(89), entry.Key);
        Assert.Equal(7m, entry.Value);
    }

    /// <summary>Bloques nulos (el día no cargó ítems) devuelven vacío, no null ni excepción.</summary>
    [Fact]
    public void LosDosBloquesEnNull_DevuelveDiccionarioVacio()
    {
        Assert.Empty(ItemConsumoCalculos.AcumularPorOrigen(null, null));
    }

    // ── Conversión de unidad ────────────────────────────────────────────────────────────────

    /// <summary>
    /// SOLO gramos convierte. Todo lo demás —incluida una unidad desconocida como <c>'l'</c>,
    /// <c>'lb'</c> o <c>'qq'</c>— se asume ya en kg y pasa el número tal cual.
    /// <para>
    /// NOTA: eso significa que una libra o un galón se descuentan como si fueran kilos. Es el
    /// comportamiento vigente en producción y este test lo <b>fija</b>, no lo aprueba: cambiarlo
    /// movería el saldo histórico de todas las empresas y es otra fase con su propio testigo.
    /// La unidad real del stock la manda el catálogo, no este campo.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("g", 500, 0.5)]
    [InlineData("gramos", 500, 0.5)]
    [InlineData("gramo", 2000, 2)]
    [InlineData("G", 500, 0.5)]          // se normaliza a minúsculas
    [InlineData("  g  ", 500, 0.5)]      // y se recorta
    [InlineData("kg", 320, 320)]
    [InlineData("l", 30, 30)]            // litros: NO se convierte
    [InlineData("lb", 10, 10)]           // libras: NO se convierte
    [InlineData("qq", 2, 2)]             // quintales: NO se convierte
    [InlineData("unidades", 12, 12)]
    public void SoloGramosSeConvierte_ElRestoSeAsumeKg(string unidad, double cantidad, double esperadoKg)
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[] { Item(itemInventarioEcuadorId: 208, cantidad: cantidad, unidad: unidad) }, null);

        Assert.Equal((decimal)esperadoKg, r[Inventario(208)]);
    }

    // ── Acumulación: se SUMAN, el último no pisa ────────────────────────────────────────────

    /// <summary>
    /// Dos filas del mismo ítem en el MISMO bloque se acumulan. Si el último pisara al anterior, un
    /// registro con el alimento partido en dos renglones descontaría solo el segundo.
    /// </summary>
    [Fact]
    public void DosItemsConElMismoId_SeAcumulanNoSePisan()
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[]
            {
                Item(itemInventarioEcuadorId: 208, cantidad: 300),
                Item(itemInventarioEcuadorId: 208, cantidad: 20)
            }, null);

        var entry = Assert.Single(r);
        Assert.Equal(320m, entry.Value);
    }

    /// <summary>
    /// El mismo ítem cargado en hembras y en machos es UN solo consumo: el descuento va contra una
    /// única fila de stock, así que las dos cantidades se suman en la misma clave.
    /// </summary>
    [Fact]
    public void MismoItemEnHembrasYEnMachos_SeSumaEnUnaSolaClave()
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[] { Item(itemInventarioEcuadorId: 10, cantidad: 5) },
            new[] { Item(itemInventarioEcuadorId: 10, cantidad: 3) });

        var entry = Assert.Single(r);
        Assert.Equal(8m, entry.Value);
    }

    /// <summary>Unidades mezcladas en la misma clave: cada fila se convierte antes de sumar.</summary>
    [Fact]
    public void UnidadesMezcladasEnLaMismaClave_SeConviertenAntesDeSumar()
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[]
            {
                Item(itemInventarioEcuadorId: 208, cantidad: 300, unidad: "kg"),
                Item(itemInventarioEcuadorId: 208, cantidad: 2000, unidad: "g")
            }, null);

        Assert.Equal(302m, r[Inventario(208)]);
    }

    // ── Silo ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El mismo ítem en dos silos distintos son DOS consumos: cada uno descuenta su propia fila de
    /// <c>inventario_gestion_stock</c>. Aplanarlos sumaría los kg y los descontaría todos del primero
    /// — el silo se quedaría en rojo y el otro nunca bajaría.
    /// </summary>
    [Fact]
    public void MismoItemEnSilosDistintos_NoSeColapsanEnUnaSolaClave()
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[]
            {
                Item(itemInventarioEcuadorId: 150, cantidad: 320, siloId: 4),
                Item(itemInventarioEcuadorId: 150, cantidad: 180, siloId: 20)
            }, null);

        Assert.Equal(2, r.Count);
        Assert.Equal(320m, r[Inventario(150, 4)]);
        Assert.Equal(180m, r[Inventario(150, 20)]);
        Assert.False(r.ContainsKey(Inventario(150)));
    }

    /// <summary>
    /// Silo ausente, 0 o negativo = «sin silo» (stock a nivel granja). Con <c>null</c> el hash y la
    /// agrupación son exactamente los de antes de la Fase C de silos: es lo que garantiza que ninguna
    /// empresa sin el flag note el cambio.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-3)]
    public void SiloNoPositivo_LaClaveQuedaSinSilo(int? siloId)
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[] { Item(itemInventarioEcuadorId: 150, cantidad: 100, siloId: siloId) }, null);

        var entry = Assert.Single(r);
        Assert.Null(entry.Key.SiloId);
        Assert.Equal(Inventario(150), entry.Key);
    }

    /// <summary>El mismo ítem con y sin silo tampoco se colapsa: son dos ubicaciones distintas.</summary>
    [Fact]
    public void MismoItemConSiloYSinSilo_SonDosClaves()
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[] { Item(itemInventarioEcuadorId: 150, cantidad: 10, siloId: 4) },
            new[] { Item(itemInventarioEcuadorId: 150, cantidad: 90) });

        Assert.Equal(2, r.Count);
        Assert.Equal(10m, r[Inventario(150, 4)]);
        Assert.Equal(90m, r[Inventario(150)]);
    }

    // ── Orden de iteración ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// El orden es hembras → machos, y dentro de cada bloque el de la lista. No cambia el total, pero
    /// sí el orden de inserción del diccionario, y ese es el orden en el que terminan escribiéndose
    /// las filas de movimiento de inventario. Fijarlo es lo que permite comparar un antes/después.
    /// </summary>
    [Fact]
    public void ElOrdenDeInsercionEsHembrasLuegoMachos()
    {
        var r = ItemConsumoCalculos.AcumularPorOrigen(
            new[] { Item(itemInventarioEcuadorId: 3, cantidad: 1), Item(itemInventarioEcuadorId: 1, cantidad: 1) },
            new[] { Item(itemInventarioEcuadorId: 2, cantidad: 1) });

        Assert.Equal(new[] { Inventario(3), Inventario(1), Inventario(2) }, r.Keys.ToArray());
    }

    // ── Equivalencia con la ruta que lee del metadata ───────────────────────────────────────

    /// <summary>
    /// <b>El contrato central.</b> Los kg que se descuentan al CREAR (leyendo los DTOs del request)
    /// tienen que ser los mismos que se devuelven al EDITAR o BORRAR (leyendo el metadata jsonb ya
    /// persistido). Si las dos rutas divergen, una edición devuelve al stock una cantidad distinta de
    /// la que se descontó y el saldo del galpón queda mal para siempre.
    /// </summary>
    [Fact]
    public void EquivalenciaConParseMetadata_MismoResultadoPorLasDosRutas()
    {
        var hembras = new[]
        {
            Item(catalogItemId: 89, itemInventarioEcuadorId: 208, cantidad: 300, unidad: "kg", siloId: 4),
            Item(catalogItemId: 90, cantidad: 2000, unidad: "g"),
            Item(catalogItemId: 0, cantidad: 999),                                   // fila vacía: se ignora
            Item(catalogItemId: 89, itemInventarioEcuadorId: 208, cantidad: 20, unidad: "kg", siloId: 4)
        };
        var machos = new[]
        {
            Item(itemInventarioEcuadorId: 208, cantidad: 15, unidad: "kg", siloId: 20),
            Item(catalogItemId: 90, cantidad: 1, unidad: "kg")
        };

        var porRequest = ItemConsumoCalculos.AcumularPorOrigen(hembras, machos);
        var porMetadata = MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(AMetadata(hembras, machos));

        Assert.Equal(porMetadata.Count, porRequest.Count);
        foreach (var kv in porMetadata)
            Assert.Equal(kv.Value, porRequest[kv.Key]);
    }

    /// <summary>Bloques ausentes en el metadata ⇄ bloques nulos en el request: los dos dan vacío.</summary>
    [Fact]
    public void EquivalenciaConParseMetadata_SinItems()
    {
        Assert.Empty(ItemConsumoCalculos.AcumularPorOrigen(null, null));
        Assert.Empty(MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(AMetadata(null, null)));
    }

    /// <summary>
    /// ⚠️ <b>Límite conocido de la equivalencia.</b> <c>ParseMetadataItemsToKgPorOrigen</c> acumula un
    /// TERCER bloque, <c>itemsGenerales</c>, que <c>CrearSeguimientoRequest</c> no declara. Si ese
    /// bloque aparece en el metadata (lo escribe la ruta de Colombia), la ruta del metadata cuenta más
    /// kilos que la del request.
    /// <para>
    /// Este test lo <b>documenta</b> en vez de afirmar una igualdad falsa. NOTA: no es un bug de este
    /// cálculo — es la señal de que si algún día producción empieza a mandar generales por el request,
    /// hay que sumarlos acá o el descuento quedará corto.
    /// </para>
    /// </summary>
    [Fact]
    public void ConItemsGenerales_LaEquivalenciaNoAplica_ElMetadataCuentaDeMas()
    {
        var hembras = new[] { Item(itemInventarioEcuadorId: 208, cantidad: 100) };
        var generales = new[] { Item(catalogItemId: 77, cantidad: 5) };

        var porRequest = ItemConsumoCalculos.AcumularPorOrigen(hembras, null);
        var porMetadata = MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(
            AMetadata(hembras, null, generales));

        Assert.Single(porRequest);
        Assert.Equal(2, porMetadata.Count);
        Assert.Equal(5m, porMetadata[Catalogo(77)]);
        Assert.False(porRequest.ContainsKey(Catalogo(77)));
    }
}
