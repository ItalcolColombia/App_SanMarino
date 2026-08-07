using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// El nombre de lote es único por <b>galpón</b>, no por granja: repetirlo en otro galpón de la misma
/// granja es el patrón legítimo de los sublotes (A374A en G0326 y en G0324 de LA ESMERALDA). Los casos
/// están numerados como en §4 del plan
/// <c>fase_de_desarrollo/lote_nombre_duplicado_por_galpon_plan.md</c>.
/// </summary>
public class LoteNombreDuplicadoCalculosTests
{
    // ── 1. El caso real que la guarda por granja bloqueaba ─────────────────────
    [Fact]
    public void Caso1_mismo_nombre_en_otro_galpon_se_permite()
    {
        // A374A entrando a G0324 con el A374A de G0326 ya activo (lotes 116 y 114).
        Assert.False(LoteNombreDuplicadoCalculos.HayDuplicado("G0324", new[] { "G0326" }));
    }

    // ── 2-4. Choque real: mismo galpón ─────────────────────────────────────────
    [Fact]
    public void Caso2_mismo_nombre_en_el_mismo_galpon_se_rechaza() =>
        Assert.True(LoteNombreDuplicadoCalculos.HayDuplicado("G0326", new[] { "G0326" }));

    [Theory]
    [InlineData("g0326")]
    [InlineData("G0326")]
    public void Caso3_la_comparacion_de_galpon_ignora_mayusculas(string galponExistente) =>
        Assert.True(LoteNombreDuplicadoCalculos.HayDuplicado("G0326", new[] { galponExistente }));

    [Fact]
    public void Caso4_los_espacios_alrededor_del_galpon_no_crean_un_grupo_nuevo() =>
        Assert.True(LoteNombreDuplicadoCalculos.HayDuplicado("  G0326  ", new[] { "G0326" }));

    // ── 5-7. Lotes sin galpón: grupo propio ────────────────────────────────────
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Caso5_lote_sin_galpon_no_choca_con_uno_que_si_tiene(string? galponNuevo) =>
        Assert.False(LoteNombreDuplicadoCalculos.HayDuplicado(galponNuevo, new[] { "G0326" }));

    [Fact]
    public void Caso6_dos_lotes_sin_galpon_con_el_mismo_nombre_chocan() =>
        Assert.True(LoteNombreDuplicadoCalculos.HayDuplicado(null, new string?[] { null }));

    [Fact]
    public void Caso6b_vacio_y_null_son_el_mismo_grupo() =>
        Assert.True(LoteNombreDuplicadoCalculos.HayDuplicado("", new string?[] { "   " }));

    [Fact]
    public void Caso7_lote_con_galpon_no_choca_con_uno_sin_galpon() =>
        Assert.False(LoteNombreDuplicadoCalculos.HayDuplicado("G0326", new string?[] { null }));

    // ── 8. Sin homónimos ───────────────────────────────────────────────────────
    [Fact]
    public void Caso8_sin_homonimos_no_hay_duplicado() =>
        Assert.False(LoteNombreDuplicadoCalculos.HayDuplicado("G0326", Array.Empty<string?>()));

    [Fact]
    public void Caso8b_varios_homonimos_en_otros_galpones_siguen_sin_chocar() =>
        Assert.False(LoteNombreDuplicadoCalculos.HayDuplicado("G0324", new[] { "G0326", "G0325", "G0323" }));

    [Fact]
    public void Caso8c_basta_un_homonimo_en_el_mismo_galpon_para_chocar() =>
        Assert.True(LoteNombreDuplicadoCalculos.HayDuplicado("G0325", new[] { "G0326", "G0325", "G0323" }));

    // ── 9. Normalización del nombre ────────────────────────────────────────────
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("  A374A  ", "A374A")]
    public void Caso9_el_nombre_se_normaliza_con_trim(string? entrada, string esperado) =>
        Assert.Equal(esperado, LoteNombreDuplicadoCalculos.NormalizarNombre(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Caso9b_galpon_vacio_se_normaliza_a_null(string? entrada) =>
        Assert.Null(LoteNombreDuplicadoCalculos.NormalizarGalpon(entrada));

    [Fact]
    public void Caso9c_galpon_informado_conserva_su_valor_sin_espacios() =>
        Assert.Equal("G0326", LoteNombreDuplicadoCalculos.NormalizarGalpon("  G0326 "));

    // ── 10. Mensajes ───────────────────────────────────────────────────────────
    [Fact]
    public void Caso10_el_mensaje_con_galpon_habla_del_galpon()
    {
        var msg = LoteNombreDuplicadoCalculos.MensajeDuplicado("  A374A ", "G0326");
        Assert.Equal("Ya existe un lote activo con el nombre 'A374A' en este galpón.", msg);
    }

    [Fact]
    public void Caso10b_el_mensaje_sin_galpon_lo_dice_explicitamente()
    {
        var msg = LoteNombreDuplicadoCalculos.MensajeDuplicado("A374A", null);
        Assert.Equal("Ya existe un lote activo sin galpón con el nombre 'A374A' en esta granja.", msg);
    }
}
