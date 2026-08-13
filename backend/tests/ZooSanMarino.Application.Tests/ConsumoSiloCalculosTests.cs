using System.Text.Json;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Fase C del plan de silos — el consumo del seguimiento diario sale de un silo concreto.
///
/// <para>
/// Los casos 6-8 del plan (§10.2) son el contrato de la clave: <b>sin</b> <c>siloId</c> el hash y la
/// agrupación tienen que ser los de siempre —es lo que garantiza que ninguna empresa sin el flag note
/// el cambio—, y <b>con</b> <c>siloId</c> el mismo ítem en dos silos son dos consumos distintos.
/// </para>
/// </summary>
public class ConsumoSiloCalculosTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    // ── Caso 6 — metadata sin siloId: la clave es la de antes de la Fase C ───────────────────

    [Fact]
    public void Caso6_SinSiloId_ClaveIdenticaALaDeAntes()
    {
        var root = Parse(@"{
            ""itemsHembras"": [ { ""itemInventarioEcuadorId"": 150, ""cantidad"": 320, ""unidad"": ""kg"" } ],
            ""itemsMachos"":  [ { ""catalogItemId"": 89, ""cantidad"": 40, ""unidad"": ""kg"" } ]
        }");

        var r = MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(root);

        Assert.Equal(320m, r[new ItemConsumoKey(150, true)]);
        Assert.Equal(40m, r[new ItemConsumoKey(89, false)]);
        Assert.Equal(2, r.Count);
        // La clave de dos parámetros y la de tres con silo null son LA MISMA (hash incluido): es lo
        // que hace que un metadata viejo siga resolviendo contra el mismo stock.
        Assert.Equal(new ItemConsumoKey(150, true), new ItemConsumoKey(150, true, null));
        Assert.Equal(new ItemConsumoKey(150, true).GetHashCode(), new ItemConsumoKey(150, true, null).GetHashCode());
    }

    [Fact]
    public void Caso6b_SiloIdNuloOCero_SeTrataComoSinSilo()
    {
        // Un form a medio llenar manda null o 0. Ninguno de los dos puede inventar el silo 0.
        var root = Parse(@"{
            ""itemsHembras"": [ { ""itemInventarioEcuadorId"": 150, ""siloId"": null, ""cantidad"": 10, ""unidad"": ""kg"" },
                                { ""itemInventarioEcuadorId"": 151, ""siloId"": 0, ""cantidad"": 5, ""unidad"": ""kg"" } ]
        }");

        var r = MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(root);

        Assert.Equal(10m, r[new ItemConsumoKey(150, true)]);
        Assert.Equal(5m, r[new ItemConsumoKey(151, true)]);
    }

    // ── Caso 7 — mismo ítem en dos silos: dos claves ────────────────────────────────────────

    [Fact]
    public void Caso7_MismoItemEnDosSilos_SonDosClavesConSusKg()
    {
        var root = Parse(@"{
            ""itemsHembras"": [ { ""itemInventarioEcuadorId"": 150, ""siloId"": 4, ""cantidad"": 320, ""unidad"": ""kg"" },
                                { ""itemInventarioEcuadorId"": 150, ""siloId"": 20, ""cantidad"": 180, ""unidad"": ""kg"" } ]
        }");

        var r = MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(root);

        Assert.Equal(2, r.Count);
        Assert.Equal(320m, r[new ItemConsumoKey(150, true, 4)]);
        Assert.Equal(180m, r[new ItemConsumoKey(150, true, 20)]);
        Assert.False(r.ContainsKey(new ItemConsumoKey(150, true)));   // no se aplanan en una sola
    }

    // ── Caso 8 — mismo ítem, mismo silo, hembras + machos: una clave, kg sumados ────────────

    [Fact]
    public void Caso8_MismoItemMismoSilo_HembrasYMachos_SeSuman()
    {
        var root = Parse(@"{
            ""itemsHembras"": [ { ""itemInventarioEcuadorId"": 150, ""siloId"": 4, ""cantidad"": 300, ""unidad"": ""kg"" } ],
            ""itemsMachos"":  [ { ""itemInventarioEcuadorId"": 150, ""siloId"": 4, ""cantidad"": 2000, ""unidad"": ""g"" } ]
        }");

        var r = MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(root);

        Assert.Single(r);
        Assert.Equal(302m, r[new ItemConsumoKey(150, true, 4)]);   // 300 kg + 2000 g
    }

    [Fact]
    public void ParserPlano_NoAprendeElSilo_EcuadorYPanamaIntactos()
    {
        // La variante plana es la de Ecuador/Panamá y el plan dice explícitamente que no se toca:
        // sigue agrupando por id, aunque las filas traigan silos distintos.
        var root = Parse(@"{
            ""itemsHembras"": [ { ""itemInventarioEcuadorId"": 150, ""siloId"": 4, ""cantidad"": 300, ""unidad"": ""kg"" },
                                { ""itemInventarioEcuadorId"": 150, ""siloId"": 20, ""cantidad"": 200, ""unidad"": ""kg"" } ]
        }");

        var r = MetadataEngordeCalculos.ParseMetadataItemsToKg(root);

        Assert.Single(r);
        Assert.Equal(500m, r[150]);
    }

    // ── Validación de las claves contra los silos del lote ──────────────────────────────────

    [Fact]
    public void FlagOff_SinSilos_NoHayError()
    {
        var claves = new[] { new ItemConsumoKey(150, true), new ItemConsumoKey(89, false) };

        Assert.Null(ConsumoSiloCalculos.ValidarClaves(ModoUbicacionInventario.Clasico, claves, []));
    }

    [Fact]
    public void FlagOff_ConSilo_SeRechaza_NoSeMezclanLosModelos()
    {
        var claves = new[] { new ItemConsumoKey(150, true, 4) };

        Assert.Equal(
            ConsumoSiloCalculos.MensajeSiloNoAplica,
            ConsumoSiloCalculos.ValidarClaves(ModoUbicacionInventario.Clasico, claves, []));
    }

    [Fact]
    public void FlagOn_SinSilo_ExigeElSilo()
    {
        var claves = new[] { new ItemConsumoKey(150, true) };

        Assert.Equal(
            ConsumoSiloCalculos.MensajeSiloRequerido,
            ConsumoSiloCalculos.ValidarClaves(ModoUbicacionInventario.PorSilo, claves, [4, 20]));
    }

    [Fact]
    public void FlagOn_SiloDelLote_Pasa()
    {
        var claves = new[] { new ItemConsumoKey(150, true, 4), new ItemConsumoKey(151, true, 20) };

        Assert.Null(ConsumoSiloCalculos.ValidarClaves(ModoUbicacionInventario.PorSilo, claves, [4, 20]));
    }

    [Fact]
    public void Caso19_FlagOn_SiloNoAsignadoAlLote_SeRechaza()
    {
        var claves = new[] { new ItemConsumoKey(150, true, 4), new ItemConsumoKey(151, true, 99) };

        var error = ConsumoSiloCalculos.ValidarClaves(ModoUbicacionInventario.PorSilo, claves, [4, 20]);

        Assert.Equal(ConsumoSiloCalculos.MensajeSiloNoAsignadoAlLote(99), error);
        Assert.Contains("99", error);
    }

    [Fact]
    public void FlagOn_LoteSinSilosAsignados_RechazaEnVezDeDejarPasar()
    {
        var claves = new[] { new ItemConsumoKey(150, true, 4) };

        Assert.Equal(
            ConsumoSiloCalculos.MensajeSiloNoAsignadoAlLote(4),
            ConsumoSiloCalculos.ValidarClaves(ModoUbicacionInventario.PorSilo, claves, []));
    }

    [Fact]
    public void SinClaves_NoValidaNada_EnNingunModo()
    {
        // Un día sin alimento se registra igual, con flag y sin flag.
        Assert.Null(ConsumoSiloCalculos.ValidarClaves(ModoUbicacionInventario.PorSilo, [], []));
        Assert.Null(ConsumoSiloCalculos.ValidarClaves(ModoUbicacionInventario.Clasico, [], []));
    }

    [Fact]
    public void SilosReferidos_SinRepetir_IgnoraLasClavesSinSilo()
    {
        var claves = new[]
        {
            new ItemConsumoKey(150, true, 4),
            new ItemConsumoKey(151, true, 4),
            new ItemConsumoKey(152, true, 20),
            new ItemConsumoKey(153, true)
        };

        Assert.Equal([4, 20], ConsumoSiloCalculos.SilosReferidos(claves));
    }
}
