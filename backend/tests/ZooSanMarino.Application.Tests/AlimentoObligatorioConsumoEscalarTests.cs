using System.Text.Json;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// El alimento se puede mandar de <b>dos</b> formas y las dos tienen que valer: como ítems del
/// metadata (el formulario web) o como campo suelto de consumo (la app móvil, la carga masiva por
/// Excel y la PWA, que no pasan por la pantalla).
///
/// <para>
/// <b>El bug que cierran estos tests (21-ago-2026):</b> Reproductora y Producción llamaban al guard
/// sin pasarle los kilos sueltos. Como <see cref="MetadataEngordeCalculos.ParseKgPorBloque"/> sólo
/// suma ítems con <c>catalogItemId</c> / <c>itemInventarioEcuadorId</c> &gt; 0, un registro con
/// <c>consumoHembras: 120</c> y sin ítems de inventario contaba CERO kilos y el backend respondía
/// 400 «no tiene alimento» sobre un registro que sí lo traía. Medido en Panamá —la única empresa con
/// <c>requiere_validacion_seguimiento_diario</c> en true—; con el flag apagado el mismo request se
/// creaba bien, o sea que el bloqueo era esa rama y no el payload.
/// </para>
/// </summary>
public class AlimentoObligatorioConsumoEscalarTests
{
    /// <summary>
    /// El metadata tal cual lo manda un cliente que NO pasa por el formulario: el consumo va en el
    /// campo suelto y los ítems, si están, vienen sin ítem de inventario seleccionado.
    /// </summary>
    private static JsonElement MetadataEscalar(double? consumoHembras = null, double? consumoMachos = null)
    {
        var doc = JsonDocument.Parse($$"""
        {
          "consumoHembras": {{(consumoHembras?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")}},
          "unidadConsumoHembras": "kg",
          "consumoMachos": {{(consumoMachos?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")}},
          "unidadConsumoMachos": "kg",
          "itemsHembras": [],
          "itemsMachos": []
        }
        """);
        return doc.RootElement;
    }

    /// <summary>Metadata del formulario web: ítems con ítem de inventario elegido.</summary>
    private static JsonElement MetadataConItems(decimal kgHembras = 0, decimal kgMachos = 0, decimal kgGenerales = 0)
    {
        string Arr(decimal kg) => kg > 0
            ? $$"""[{ "catalogItemId": 7, "cantidad": {{kg.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "unidad": "kg" }]"""
            : "[]";

        var doc = JsonDocument.Parse($$"""
        {
          "itemsHembras": {{Arr(kgHembras)}},
          "itemsMachos": {{Arr(kgMachos)}},
          "itemsGenerales": {{Arr(kgGenerales)}}
        }
        """);
        return doc.RootElement;
    }

    // ─── La causa raíz, escrita ───────────────────────────────────────────────

    [Fact]
    public void ParseKgPorBloque_SobreElPayloadEscalar_DaCero()
    {
        // Esto NO es un defecto de ParseKgPorBloque: sin ítem seleccionado no hay ítem que descontar.
        // Es la razón por la que el guard necesita además los kilos sueltos.
        var (h, m, g) = MetadataEngordeCalculos.ParseKgPorBloque(MetadataEscalar(consumoHembras: 120));

        Assert.Equal(0m, h);
        Assert.Equal(0m, m);
        Assert.Equal(0m, g);
    }

    // ─── El fix: el consumo escalar cumple ────────────────────────────────────

    [Theory]
    [InlineData(ModuloSeguimiento.Reproductora)]
    [InlineData(ModuloSeguimiento.Produccion)]
    [InlineData(ModuloSeguimiento.Levante)]
    public void ConsumoEscalarSinItems_Cumple(string modulo)
    {
        // El caso exacto que devolvía 400 en Panamá.
        var capturado = AlimentoObligatorioCalculos.Capturado(
            MetadataEscalar(consumoHembras: 120), kgHembrasDirecto: 120m, kgMachosDirecto: 0m);

        Assert.Equal(120m, capturado.KgHembras);
        Assert.Null(AlimentoObligatorioCalculos.Motivo(
            modulo, loteEsMixto: false, capturado, new DateOnly(2026, 8, 21)));
    }

    [Fact]
    public void ConsumoEscalarSoloEnMachos_Cumple()
    {
        // «Alguno de los géneros, macho o hembra, o los dos»: machos solo alcanza.
        var capturado = AlimentoObligatorioCalculos.Capturado(
            MetadataEscalar(consumoMachos: 30), kgHembrasDirecto: 0m, kgMachosDirecto: 30m);

        Assert.True(AlimentoObligatorioCalculos.Cumple(
            ModuloSeguimiento.Produccion, loteEsMixto: false, capturado));
    }

    [Fact]
    public void SinMetadata_PeroConConsumoEscalar_Cumple()
    {
        // Reproductora acepta el registro sin metadata; los kilos igual tienen que valer.
        var capturado = AlimentoObligatorioCalculos.Capturado(null, kgHembrasDirecto: 45.5m, kgMachosDirecto: 0m);

        Assert.Equal(45.5m, capturado.KgHembras);
        Assert.True(AlimentoObligatorioCalculos.Cumple(ModuloSeguimiento.Reproductora, false, capturado));
    }

    [Fact]
    public void EngordeEnMixto_ConEscalar_Cumple()
    {
        // En modo Mixto el formulario vuelca la captura en el bloque de hembras.
        var capturado = AlimentoObligatorioCalculos.Capturado(
            MetadataEscalar(consumoHembras: 850), kgHembrasDirecto: 850m, kgMachosDirecto: 0m);

        Assert.True(AlimentoObligatorioCalculos.Cumple(ModuloSeguimiento.Engorde, loteEsMixto: true, capturado));
    }

    // ─── Lo que NO cambia: sin alimento sigue rechazando, con el mismo texto ──

    [Theory]
    [InlineData(ModuloSeguimiento.Reproductora)]
    [InlineData(ModuloSeguimiento.Produccion)]
    public void SinConsumoNiItems_Rechaza(string modulo)
    {
        var capturado = AlimentoObligatorioCalculos.Capturado(MetadataEscalar(), 0m, 0m);

        Assert.False(AlimentoObligatorioCalculos.Cumple(modulo, false, capturado));
    }

    [Fact]
    public void SinConsumoNiItems_ElMensajeEsElMismoDeHoy()
    {
        // Comparación LITERAL: el fix no puede mover ni una letra del rechazo. Los textos salen de
        // AlimentoObligatorioCalculos.BloqueExigido y son los que el usuario ya conoce.
        var vacio = AlimentoObligatorioCalculos.Capturado(MetadataEscalar(), 0m, 0m);
        var fecha = new DateOnly(2026, 8, 21);

        Assert.Equal(
            "El registro del 21/08/2026 no tiene alimento: hay que indicar el tipo de alimento y la "
            + "cantidad de consumo del lote.",
            AlimentoObligatorioCalculos.Motivo(ModuloSeguimiento.Reproductora, false, vacio, fecha));

        Assert.Equal(
            "El registro del 21/08/2026 no tiene alimento: hay que indicar el tipo de alimento y la "
            + "cantidad de consumo en Hembras, en Machos o en ambos.",
            AlimentoObligatorioCalculos.Motivo(ModuloSeguimiento.Produccion, false, vacio, fecha));
    }

    [Fact]
    public void SoloGenerales_SigueSinContar_YElMotivoLoDice()
    {
        // Los «otros ítems» (vitaminas, insumos) no son consumo de alimento. Pasarle los directos al
        // guard no puede abrir esa puerta: acá los directos son 0.
        var capturado = AlimentoObligatorioCalculos.Capturado(
            MetadataConItems(kgGenerales: 500m), kgHembrasDirecto: 0m, kgMachosDirecto: 0m);

        Assert.Equal(500m, capturado.KgGenerales);
        Assert.False(AlimentoObligatorioCalculos.Cumple(ModuloSeguimiento.Produccion, false, capturado));
        Assert.Contains("otros ítems",
            AlimentoObligatorioCalculos.Motivo(ModuloSeguimiento.Produccion, false, capturado, null));
    }

    // ─── MÁXIMO, no suma ──────────────────────────────────────────────────────

    [Fact]
    public void ItemsYEscalarDelMismoAlimento_SeTomaElMaximo_NoLaSuma()
    {
        // El formulario web llena el campo suelto ADEMÁS de los ítems: son el mismo alimento contado
        // dos veces. Sumarlos inventaría kilos que nadie cargó.
        var capturado = AlimentoObligatorioCalculos.Capturado(
            MetadataConItems(kgHembras: 120m), kgHembrasDirecto: 120m, kgMachosDirecto: 0m);

        Assert.Equal(120m, capturado.KgHembras);
    }

    [Fact]
    public void ItemsMayoresQueElEscalar_GananLosItems()
    {
        var capturado = AlimentoObligatorioCalculos.Capturado(
            MetadataConItems(kgHembras: 200m, kgMachos: 50m), kgHembrasDirecto: 120m, kgMachosDirecto: 0m);

        Assert.Equal(200m, capturado.KgHembras);
        Assert.Equal(50m, capturado.KgMachos);
    }

    [Fact]
    public void EscalarMayorQueLosItems_GanaElEscalar()
    {
        var capturado = AlimentoObligatorioCalculos.Capturado(
            MetadataConItems(kgHembras: 10m), kgHembrasDirecto: 120m, kgMachosDirecto: 0m);

        Assert.Equal(120m, capturado.KgHembras);
    }

    // ─── Machos null ──────────────────────────────────────────────────────────

    [Fact]
    public void MachosSinValor_ValeCero_YNoCambiaLaDecision()
    {
        // Los services pasan `(decimal)(dto.ConsumoKgMachos ?? 0)`: `ConsumoKgMachos` es double? y
        // llega null siempre que el registro no tiene alimento de machos (todo Panamá mixto).
        // Con el `!` de antes —(decimal)x!— la conversión DESENVUELVE y lanza
        // InvalidOperationException («Nullable object must have a value»), que el controller traduce
        // a un 400 ilegible.
        double? machosNull = null;

        var capturado = AlimentoObligatorioCalculos.Capturado(
            MetadataEscalar(consumoHembras: 120), 120m, (decimal)(machosNull ?? 0));

        Assert.Equal(0m, capturado.KgMachos);
        Assert.True(AlimentoObligatorioCalculos.Cumple(ModuloSeguimiento.Reproductora, false, capturado));
    }

    // ─── Con el flag apagado nada de esto corre ───────────────────────────────

    [Fact]
    public void FlagApagado_NoSeSeparaYPorLoTantoNoSeValida()
    {
        // Los cinco services envuelven la validación en `if (separa)`. Sin el flag de empresa,
        // `separa` es false y el guard ni se llama: para las empresas que no están en doble
        // validación este trabajo es un no-op.
        Assert.False(ValidacionSeguimientoCalculos.SeparaAlGuardar(empresaRequiereValidacion: false));
        Assert.True(ValidacionSeguimientoCalculos.DescuentaAlGuardar(empresaRequiereValidacion: false));
    }

    [Fact]
    public void FlagEncendido_SeSeparaYSeValida()
    {
        Assert.True(ValidacionSeguimientoCalculos.SeparaAlGuardar(empresaRequiereValidacion: true));
        Assert.False(ValidacionSeguimientoCalculos.DescuentaAlGuardar(empresaRequiereValidacion: true));
    }
}
