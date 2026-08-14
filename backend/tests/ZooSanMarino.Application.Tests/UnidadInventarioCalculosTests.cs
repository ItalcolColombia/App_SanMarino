using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de TK-2026-000019 (`fase_de_desarrollo/unidad_medida_stock_inventario_plan.md`).
///
/// El defecto: el stock guardaba su propia unidad con default `kg` y nunca la sincronizaba con la
/// del catálogo, así que un producto creado en litros salía en kilos en la pantalla de Stock. Lo que
/// se prueba acá es la regla que reemplaza a ese default: <b>manda el catálogo</b>.
/// </summary>
public class UnidadInventarioCalculosTests
{
    // ─── Resolver: el catálogo manda ───────────────────────────────────────────

    [Theory]
    [InlineData("l", "kg", "l")]
    [InlineData("ml", "kg", "ml")]
    [InlineData("und", "GALONES", "und")]
    [InlineData("kg", "LT", "kg")]
    public void Resolver_prefiere_siempre_la_unidad_del_catalogo(string catalogo, string solicitada, string esperada) =>
        Assert.Equal(esperada, UnidadInventarioCalculos.Resolver(catalogo, solicitada));

    [Fact]
    public void Resolver_recorta_espacios_del_catalogo() =>
        Assert.Equal("l", UnidadInventarioCalculos.Resolver("  l  ", "kg"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolver_cae_a_la_solicitada_si_el_catalogo_no_tiene_unidad(string? catalogo) =>
        Assert.Equal("saco", UnidadInventarioCalculos.Resolver(catalogo, " saco "));

    [Fact]
    public void Resolver_cae_a_kg_cuando_no_hay_ninguna() =>
        Assert.Equal("kg", UnidadInventarioCalculos.Resolver(null, null));

    [Fact]
    public void Resolver_sin_solicitada_devuelve_la_del_catalogo() =>
        Assert.Equal("l", UnidadInventarioCalculos.Resolver("l"));

    [Fact]
    public void UnidadPorDefecto_sigue_siendo_kg() =>
        Assert.Equal("kg", UnidadInventarioCalculos.UnidadPorDefecto);

    // ─── EstaDesalineada: detecta la fila que hay que realinear ────────────────

    [Theory]
    [InlineData("kg", "l")]      // el caso del ticket: stock kg, catálogo litros
    [InlineData("LT", "und")]
    [InlineData("", "l")]
    public void EstaDesalineada_true_cuando_la_fila_no_coincide_con_el_catalogo(string fila, string catalogo) =>
        Assert.True(UnidadInventarioCalculos.EstaDesalineada(fila, catalogo));

    [Theory]
    [InlineData("l", "l")]
    [InlineData("L", "l")]       // la corrección manual entró con otra capitalización: NO es divergencia
    [InlineData(" kg ", "kg")]
    public void EstaDesalineada_false_cuando_coincide_salvo_caja_o_espacios(string fila, string catalogo) =>
        Assert.False(UnidadInventarioCalculos.EstaDesalineada(fila, catalogo));

    [Fact]
    public void EstaDesalineada_sin_unidad_en_el_catalogo_se_queda_con_la_de_la_fila() =>
        Assert.False(UnidadInventarioCalculos.EstaDesalineada("LT", null));

    // ─── Normalizar: las variantes que operación tipeó a mano ──────────────────

    [Theory]
    [InlineData("LT", "l")]
    [InlineData("lts", "l")]
    [InlineData("Litros", "l")]
    [InlineData("Ml", "ml")]
    [InlineData("Gr", "g")]
    [InlineData("UND", "und")]
    [InlineData("Unidades", "und")]
    [InlineData("GALONES", "gal")]
    [InlineData("DOSIS", "dosis")]
    [InlineData("Sacos", "saco")]
    [InlineData("KGS", "kg")]
    public void Normalizar_lleva_las_variantes_legacy_al_vocabulario_del_catalogo(string entrada, string esperada) =>
        Assert.Equal(esperada, UnidadInventarioCalculos.Normalizar(entrada));

    [Theory]
    [InlineData("kg")]
    [InlineData("l")]
    [InlineData("ml")]
    [InlineData("g")]
    [InlineData("und")]
    [InlineData("lb")]
    [InlineData("saco")]
    [InlineData("dosis")]
    [InlineData("gal")]
    public void Normalizar_es_idempotente_sobre_el_vocabulario_del_catalogo(string unidad) =>
        Assert.Equal(unidad, UnidadInventarioCalculos.Normalizar(unidad));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalizar_devuelve_null_si_no_hay_unidad(string? unidad) =>
        Assert.Null(UnidadInventarioCalculos.Normalizar(unidad));

    [Fact]
    public void Normalizar_una_unidad_desconocida_solo_la_baja_a_minusculas() =>
        Assert.Equal("frasco", UnidadInventarioCalculos.Normalizar(" Frasco "));
}
