using System.Text.Json;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Clasificación de huevos POR ÍTEMS del catálogo (Primera/Pnc) — Santa Reyes, Fase 2 §6.
/// Cubre la suma que alimenta <c>huevo_tot</c>, las validaciones del desglose y la semántica
/// documentada de <c>null</c> ("no tocar") vs <c>[]</c> ("quitar la clasificación por ítems"):
/// AMBAS son entradas válidas y suman 0; la diferencia la aplica <c>ProduccionService</c>
/// (null conserva lo persistido en el metadata, [] lo elimina y vuelve a los campos sueltos).
/// </summary>
public class HuevoItemsCalculosTests
{
    private static HuevoItemSeguimientoDto Item(int catalogItemId, int cantidad, string? tipoHuevo = "Primera") =>
        new(catalogItemId, Codigo: catalogItemId.ToString(), Nombre: $"HUEVO {catalogItemId}",
            TipoHuevo: tipoHuevo, Cantidad: cantidad, Um: "UND");

    // ── SumarTotal ───────────────────────────────────────────────────────────────

    [Fact]
    public void SumarTotal_Null_SignificaSinClasificacionPorItems_Da0()
    {
        Assert.Equal(0, HuevoItemsCalculos.SumarTotal(null));
    }

    [Fact]
    public void SumarTotal_ListaVacia_SignificaQuitarClasificacionPorItems_Da0()
    {
        Assert.Equal(0, HuevoItemsCalculos.SumarTotal(new List<HuevoItemSeguimientoDto>()));
    }

    [Fact]
    public void SumarTotal_VariosItems_SumaTodasLasCantidades()
    {
        var items = new List<HuevoItemSeguimientoDto>
        {
            Item(678, 1200),               // HUEVO BLANCO (Primera)
            Item(679, 340, "Pnc"),         // HUEVO MANCHADO ROJO (Pnc)
            Item(687, 15, "Pnc")           // HUEVO RECUPERACION BOLSA KIL (Pnc)
        };

        Assert.Equal(1555, HuevoItemsCalculos.SumarTotal(items));
    }

    [Fact]
    public void SumarTotal_ItemsEnCero_NoAlteranElTotal()
    {
        var items = new List<HuevoItemSeguimientoDto> { Item(678, 500), Item(679, 0), Item(680, 0) };

        Assert.Equal(500, HuevoItemsCalculos.SumarTotal(items));
    }

    [Fact]
    public void SumarTotal_UnSoloItem_DaSuCantidad()
    {
        Assert.Equal(970, HuevoItemsCalculos.SumarTotal(new[] { Item(678, 970) }));
    }

    // ── Validar ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Validar_Null_EsValido_PorqueSignificaNoTocarLaClasificacionPorItems()
    {
        Assert.Null(HuevoItemsCalculos.Validar(null));
    }

    [Fact]
    public void Validar_ListaVacia_EsValida_PorqueSignificaQuitarLaClasificacionPorItems()
    {
        Assert.Null(HuevoItemsCalculos.Validar(new List<HuevoItemSeguimientoDto>()));
    }

    [Fact]
    public void Validar_ListaValida_DevuelveNull()
    {
        var items = new List<HuevoItemSeguimientoDto> { Item(678, 1200), Item(679, 0, "Pnc"), Item(680, 45, "Pnc") };

        Assert.Null(HuevoItemsCalculos.Validar(items));
    }

    [Fact]
    public void Validar_CantidadNegativa_DevuelveMensajeConElItem()
    {
        var items = new List<HuevoItemSeguimientoDto> { Item(678, 100), Item(679, -1, "Pnc") };

        var error = HuevoItemsCalculos.Validar(items);

        Assert.NotNull(error);
        Assert.Contains("679", error);
        Assert.Contains("negativa", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validar_CatalogItemIdInvalido_DevuelveMensaje(int catalogItemId)
    {
        var items = new List<HuevoItemSeguimientoDto> { Item(catalogItemId, 100) };

        var error = HuevoItemsCalculos.Validar(items);

        Assert.NotNull(error);
        Assert.Contains("catalogItemId", error);
    }

    [Fact]
    public void Validar_CatalogItemIdDuplicado_DevuelveMensajeDeRepetido()
    {
        var items = new List<HuevoItemSeguimientoDto> { Item(678, 100), Item(679, 50, "Pnc"), Item(678, 30) };

        var error = HuevoItemsCalculos.Validar(items);

        Assert.NotNull(error);
        Assert.Contains("678", error);
        Assert.Contains("repetido", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validar_IdInvalidoTienePrioridadSobreCantidadNegativa()
    {
        // Misma fila con los dos problemas: se reporta primero el ítem sin seleccionar.
        var items = new List<HuevoItemSeguimientoDto> { Item(0, -3) };

        var error = HuevoItemsCalculos.Validar(items);

        Assert.NotNull(error);
        Assert.Contains("catalogItemId", error);
    }

    // ── LeerDeMetadata / AMetadataJson (round-trip del jsonb) ─────────────────────

    [Fact]
    public void LeerDeMetadata_SinLaClaveHuevoItems_DevuelveListaVacia()
    {
        using var doc = JsonDocument.Parse("""{"itemsHembras":[{"catalogItemId":1,"cantidad":5}]}""");

        Assert.Empty(HuevoItemsCalculos.LeerDeMetadata(doc.RootElement));
    }

    [Fact]
    public void LeerDeMetadata_ClaveVacia_DevuelveListaVacia()
    {
        using var doc = JsonDocument.Parse("""{"huevoItems":[]}""");

        Assert.Empty(HuevoItemsCalculos.LeerDeMetadata(doc.RootElement));
    }

    [Fact]
    public void LeerDeMetadata_DescartaFilasSinCatalogItemIdValido()
    {
        using var doc = JsonDocument.Parse(
            """{"huevoItems":[{"catalogItemId":0,"cantidad":10},{"cantidad":7},{"catalogItemId":678,"cantidad":3}]}""");

        var items = HuevoItemsCalculos.LeerDeMetadata(doc.RootElement);

        Assert.Single(items);
        Assert.Equal(678, items[0].CatalogItemId);
        Assert.Equal(3, items[0].Cantidad);
    }

    [Fact]
    public void RoundTrip_EscribirEnMetadataYLeerDeMetadata_ConservaElDesglose()
    {
        var original = new List<HuevoItemSeguimientoDto>
        {
            new(678, "2520", "HUEVO BLANCO", "Primera", 1200, "UND"),
            new(687, "1944", "HUEVO RECUPERACION BOLSA KIL", "Pnc", 15, "KIL")
        };

        using var doc = HuevoItemsCalculos.EscribirEnMetadata(null, original)!;
        var leidos = HuevoItemsCalculos.LeerDeMetadata(doc.RootElement);

        Assert.Equal(original, leidos);
        Assert.Equal(HuevoItemsCalculos.SumarTotal(original), HuevoItemsCalculos.SumarTotal(leidos));
    }

    [Fact]
    public void EscribirEnMetadata_UsaClavesCamelCaseDelContratoConElFront()
    {
        using var doc = HuevoItemsCalculos.EscribirEnMetadata(
            null, new[] { new HuevoItemSeguimientoDto(678, "2520", "HUEVO BLANCO", "Primera", 1200, "UND") })!;

        var fila = doc.RootElement.GetProperty("huevoItems")[0];

        Assert.Equal(678, fila.GetProperty("catalogItemId").GetInt32());
        Assert.Equal("2520", fila.GetProperty("codigo").GetString());
        Assert.Equal("HUEVO BLANCO", fila.GetProperty("nombre").GetString());
        Assert.Equal("Primera", fila.GetProperty("tipoHuevo").GetString());
        Assert.Equal(1200, fila.GetProperty("cantidad").GetInt32());
        Assert.Equal("UND", fila.GetProperty("um").GetString());
    }

    [Fact]
    public void EscribirEnMetadata_ConservaVerbatimLasDemasClavesDelMetadata()
    {
        using var previo = JsonDocument.Parse(
            """
            {"itemsHembras":[{"tipoItem":"alimento","catalogItemId":12,"itemInventarioEcuadorId":null,"cantidad":80.5,"unidad":"kg"}],
             "consumoOriginalHembras":80.5,"unidadConsumoOriginalHembras":"kg","tipoItemHembras":"alimento","tipoAlimentoHembras":12}
            """);

        using var nuevo = HuevoItemsCalculos.EscribirEnMetadata(previo, new[] { new HuevoItemSeguimientoDto(678, Cantidad: 900) })!;

        // La clave nueva está…
        Assert.Equal(900, HuevoItemsCalculos.SumarTotal(HuevoItemsCalculos.LeerDeMetadata(nuevo.RootElement)));
        // …y NADA de lo anterior se perdió ni cambió.
        Assert.Equal(80.5, nuevo.RootElement.GetProperty("consumoOriginalHembras").GetDouble());
        Assert.Equal("kg", nuevo.RootElement.GetProperty("unidadConsumoOriginalHembras").GetString());
        Assert.Equal("alimento", nuevo.RootElement.GetProperty("tipoItemHembras").GetString());
        Assert.Equal(12, nuevo.RootElement.GetProperty("tipoAlimentoHembras").GetInt32());
        var itemH = nuevo.RootElement.GetProperty("itemsHembras")[0];
        Assert.Equal(12, itemH.GetProperty("catalogItemId").GetInt32());
        Assert.Equal(80.5, itemH.GetProperty("cantidad").GetDouble());
        Assert.Equal("kg", itemH.GetProperty("unidad").GetString());
    }

    [Fact]
    public void EscribirEnMetadata_ListaVacia_QuitaLaClaveYConservaElResto()
    {
        using var previo = JsonDocument.Parse(
            """{"tipoItemHembras":"alimento","huevoItems":[{"catalogItemId":678,"cantidad":900}]}""");

        using var nuevo = HuevoItemsCalculos.EscribirEnMetadata(previo, Array.Empty<HuevoItemSeguimientoDto>())!;

        Assert.False(nuevo.RootElement.TryGetProperty("huevoItems", out _));
        Assert.Equal("alimento", nuevo.RootElement.GetProperty("tipoItemHembras").GetString());
    }

    [Fact]
    public void EscribirEnMetadata_ReemplazaElDesgloseAnteriorSinDuplicarLaClave()
    {
        using var previo = JsonDocument.Parse("""{"huevoItems":[{"catalogItemId":678,"cantidad":900}]}""");

        using var nuevo = HuevoItemsCalculos.EscribirEnMetadata(previo, new[] { new HuevoItemSeguimientoDto(679, Cantidad: 15) })!;

        var items = HuevoItemsCalculos.LeerDeMetadata(nuevo.RootElement);
        Assert.Single(items);
        Assert.Equal(679, items[0].CatalogItemId);
        Assert.Equal(15, items[0].Cantidad);
    }

    [Fact]
    public void EscribirEnMetadata_SinMetadataPrevioYSinItems_DevuelveNull()
    {
        Assert.Null(HuevoItemsCalculos.EscribirEnMetadata(null, null));
        Assert.Null(HuevoItemsCalculos.EscribirEnMetadata(null, Array.Empty<HuevoItemSeguimientoDto>()));
    }

    // ── Contrato JSON con el front (POST/PUT /api/Produccion/seguimiento) ─────────

    /// <summary>Mismas opciones que Program.cs: camelCase + case-insensitive.</summary>
    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void CrearSeguimientoRequest_BindeaHuevoItemsEnCamelCase()
    {
        const string payload = """
        {
          "fechaRegistro": "2026-07-25T12:00:00Z",
          "huevosTotales": 0,
          "huevosIncubables": 0,
          "tipoAlimento": "",
          "pesoHuevo": 0,
          "etapa": 1,
          "lotePosturaProduccionId": 42,
          "huevoItems": [
            { "catalogItemId": 678, "codigo": "2520", "nombre": "HUEVO BLANCO", "tipoHuevo": "Primera", "cantidad": 1200, "um": "UND" },
            { "catalogItemId": 687, "codigo": "1944", "nombre": "HUEVO RECUPERACION BOLSA KIL", "tipoHuevo": "Pnc", "cantidad": 15, "um": "KIL" }
          ]
        }
        """;

        var request = JsonSerializer.Deserialize<CrearSeguimientoRequest>(payload, ApiJson);

        Assert.NotNull(request);
        Assert.NotNull(request!.HuevoItems);
        Assert.Equal(2, request.HuevoItems!.Count);
        Assert.Equal(new HuevoItemSeguimientoDto(678, "2520", "HUEVO BLANCO", "Primera", 1200, "UND"), request.HuevoItems[0]);
        Assert.Equal(new HuevoItemSeguimientoDto(687, "1944", "HUEVO RECUPERACION BOLSA KIL", "Pnc", 15, "KIL"), request.HuevoItems[1]);
        Assert.Equal(1215, HuevoItemsCalculos.SumarTotal(request.HuevoItems));   // → huevo_tot
    }

    [Fact]
    public void CrearSeguimientoRequest_SinHuevoItems_QuedaNull_NoTocarLaClasificacionPorItems()
    {
        const string payload = """
        { "fechaRegistro": "2026-07-25T12:00:00Z", "huevosTotales": 900, "huevosIncubables": 0,
          "tipoAlimento": "", "pesoHuevo": 0, "etapa": 1, "lotePosturaProduccionId": 42 }
        """;

        var request = JsonSerializer.Deserialize<CrearSeguimientoRequest>(payload, ApiJson);

        Assert.NotNull(request);
        Assert.Null(request!.HuevoItems);
    }

    [Fact]
    public void CrearSeguimientoRequest_HuevoItemsVacio_SeDistingueDeNull_QuitarLaClasificacionPorItems()
    {
        const string payload = """
        { "fechaRegistro": "2026-07-25T12:00:00Z", "huevosTotales": 900, "huevosIncubables": 0,
          "tipoAlimento": "", "pesoHuevo": 0, "etapa": 1, "lotePosturaProduccionId": 42, "huevoItems": [] }
        """;

        var request = JsonSerializer.Deserialize<CrearSeguimientoRequest>(payload, ApiJson);

        Assert.NotNull(request);
        Assert.NotNull(request!.HuevoItems);
        Assert.Empty(request.HuevoItems!);
    }

    // ── CalcularDisponibilidad (F10 — traslado de huevos por ítems) ────────────────

    [Fact]
    public void CalcularDisponibilidad_ProducidoMenosTransferido_PorItem()
    {
        var producidos = new List<HuevoItemSeguimientoDto> { Item(528, 500), Item(2520, 300) };
        var transferidos = new List<HuevoItemSeguimientoDto> { Item(528, 120) };

        var disp = HuevoItemsCalculos.CalcularDisponibilidad(producidos, transferidos);

        Assert.Equal(380, disp.Single(d => d.CatalogItemId == 528).Cantidad);
        Assert.Equal(300, disp.Single(d => d.CatalogItemId == 2520).Cantidad);
    }

    [Fact]
    public void CalcularDisponibilidad_SumaVariosRegistrosDelMismoItem()
    {
        var producidos = new List<HuevoItemSeguimientoDto> { Item(528, 200), Item(528, 150) };
        var transferidos = new List<HuevoItemSeguimientoDto> { Item(528, 50), Item(528, 30) };

        var disp = HuevoItemsCalculos.CalcularDisponibilidad(producidos, transferidos);

        Assert.Equal(270, disp.Single().Cantidad); // (200+150) - (50+30)
    }

    [Fact]
    public void CalcularDisponibilidad_NuncaBajaDeCero()
    {
        var producidos = new List<HuevoItemSeguimientoDto> { Item(528, 100) };
        var transferidos = new List<HuevoItemSeguimientoDto> { Item(528, 130) }; // más de lo producido

        var disp = HuevoItemsCalculos.CalcularDisponibilidad(producidos, transferidos);

        Assert.Equal(0, disp.Single().Cantidad);
    }

    [Fact]
    public void CalcularDisponibilidad_ItemSinProduccionNiTransferencia_NoAparece()
    {
        var disp = HuevoItemsCalculos.CalcularDisponibilidad(
            new List<HuevoItemSeguimientoDto>(), new List<HuevoItemSeguimientoDto>());

        Assert.Empty(disp);
    }

    [Fact]
    public void CalcularDisponibilidad_ItemSoloEnTransferidos_Da0_NoNegativo()
    {
        // Caso borde: item desactivado del catálogo entre la producción y el traslado (o dato legado).
        var disp = HuevoItemsCalculos.CalcularDisponibilidad(
            new List<HuevoItemSeguimientoDto>(),
            new List<HuevoItemSeguimientoDto> { Item(999, 40) });

        var fila = Assert.Single(disp);
        Assert.Equal(999, fila.CatalogItemId);
        Assert.Equal(0, fila.Cantidad);
    }

    [Fact]
    public void CalcularDisponibilidad_ConservaCodigoNombreTipoUmDelItem()
    {
        var producidos = new List<HuevoItemSeguimientoDto> { Item(528, 100) };

        var disp = HuevoItemsCalculos.CalcularDisponibilidad(producidos, new List<HuevoItemSeguimientoDto>());

        var fila = Assert.Single(disp);
        Assert.Equal("528", fila.Codigo);
        Assert.Equal("HUEVO 528", fila.Nombre);
        Assert.Equal("Primera", fila.TipoHuevo);
        Assert.Equal("UND", fila.Um);
    }

    // ── F7.3 · ValidarPermitidos — la lista blanca de ítems del LOTE ────────────────────

    [Fact]
    public void ValidarPermitidos_LoteSinItemsDeclarados_RECHAZA_failClosed()
    {
        // El corazón de F7.3, y la decisión explícita del cliente (21-ago-2026): «si no tiene
        // asignado no aparece, el usuario tiene que editar el lote». Caer al catálogo completo acá
        // sería exactamente el control que se pidió eliminar.
        var error = HuevoItemsCalculos.ValidarPermitidos(
            new List<HuevoItemSeguimientoDto> { Item(528, 100) },
            permitidos: Array.Empty<int>());

        Assert.Equal(HuevoItemsCalculos.MensajeLoteSinItemsDeclarados, error);
    }

    [Fact]
    public void ValidarPermitidos_ItemFueraDeLaLista_RechazaNombrandoloConSuNombre()
    {
        // El mensaje tiene que decir QUÉ ítem sobra: «id 999» obliga al operario a ir a buscarlo.
        var error = HuevoItemsCalculos.ValidarPermitidos(
            new List<HuevoItemSeguimientoDto> { Item(528, 10), Item(999, 5) },
            permitidos: new[] { 528 },
            nombrePorItem: new Dictionary<int, string> { [999] = "HUEVO ENYEMADO ROJO" });

        Assert.NotNull(error);
        Assert.Contains("HUEVO ENYEMADO ROJO", error);
    }

    [Fact]
    public void ValidarPermitidos_SinNombreConocido_CaeAlIdYNoRevienta()
    {
        var error = HuevoItemsCalculos.ValidarPermitidos(
            new List<HuevoItemSeguimientoDto> { Item(999, 5) },
            permitidos: new[] { 528 });

        Assert.NotNull(error);
        Assert.Contains("999", error);
    }

    [Fact]
    public void ValidarPermitidos_TodosDentroDeLaLista_EsValido()
    {
        var error = HuevoItemsCalculos.ValidarPermitidos(
            new List<HuevoItemSeguimientoDto> { Item(528, 10), Item(530, 5) },
            permitidos: new[] { 528, 530, 531 });

        Assert.Null(error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidarPermitidos_SinItemsEnElRequest_EsValidoAunqueElLoteNoDeclareNada(bool nullEnVezDeVacio)
    {
        // Un lote sin declarar sigue pudiendo guardar un seguimiento SIN huevos. Rechazar acá
        // bloquearía el registro diario entero por una clasificación que nadie mandó.
        var items = nullEnVezDeVacio ? null : new List<HuevoItemSeguimientoDto>();

        Assert.Null(HuevoItemsCalculos.ValidarPermitidos(items, Array.Empty<int>()));
        Assert.Null(HuevoItemsCalculos.ValidarPermitidos(items, new[] { 528 }));
    }

    [Fact]
    public void ValidarPermitidos_NoTocaLaSemanticaDeValidar()
    {
        // `Validar` la comparten seguimiento, traslado y carga masiva; `ValidarPermitidos` es una
        // función APARTE justamente para no cambiarle el comportamiento a los tres a la vez. Si
        // alguien fusiona las dos, este test cae.
        var items = new List<HuevoItemSeguimientoDto> { Item(999, 5) };

        Assert.Null(HuevoItemsCalculos.Validar(items));                                  // pura: id>0, cant>=0, sin repetir
        Assert.NotNull(HuevoItemsCalculos.ValidarPermitidos(items, new[] { 528 }));       // lista blanca: sobra
    }

    // ── PesoTipoHuevo — orden de los grupos de las filas fijas ──────────────────────────

    [Fact]
    public void PesoTipoHuevo_PrimeraVaAntesQuePnc_YLoDesconocidoAlFinal()
    {
        Assert.True(HuevoItemsCalculos.PesoTipoHuevo("Primera") < HuevoItemsCalculos.PesoTipoHuevo("Pnc"));
        Assert.True(HuevoItemsCalculos.PesoTipoHuevo("Pnc") < HuevoItemsCalculos.PesoTipoHuevo("Enyemado"));
    }

    [Fact]
    public void PesoTipoHuevo_ToleraCapitalizacionYEspacios()
    {
        Assert.Equal(HuevoItemsCalculos.PesoTipoHuevo("Primera"), HuevoItemsCalculos.PesoTipoHuevo("  primera "));
        Assert.Equal(HuevoItemsCalculos.PesoTipoHuevo("Pnc"), HuevoItemsCalculos.PesoTipoHuevo("PNC"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PesoTipoHuevo_SinTipo_VaAlFinalNuncaIntercalado(string? tipo)
    {
        // Una categoría nueva del catálogo no se cuela entre Primera y Pnc sin que nadie lo decida.
        Assert.Equal(HuevoItemsCalculos.OrdenTiposHuevo.Length, HuevoItemsCalculos.PesoTipoHuevo(tipo));
    }
}
