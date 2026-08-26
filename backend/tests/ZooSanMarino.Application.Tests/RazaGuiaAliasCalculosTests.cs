using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Alias entre la grafía de raza del ERP del cliente y la de su guía genética propia. Medido en BD
/// el 24-ago-2026: 3 de las 4 razas de los lotes reales de Santa Reyes no cruzaban con la guía.
/// </summary>
public class RazaGuiaAliasCalculosTests
{
    // ── Las 2 grafías que el ERP escribe distinto ────────────────────────────
    [Theory]
    [InlineData("BABCOK BROWN", "babcock brown")]
    [InlineData("babcok brown", "babcock brown")]
    [InlineData("  BABCOK BROWN  ", "babcock brown")]
    [InlineData("HY LINE", "hy line brown")]
    [InlineData("hy line", "hy line brown")]
    public void AliasGuiaPropia_traduce_la_grafia_del_ERP(string razaErp, string esperada)
    {
        Assert.Equal(esperada, RazaGuiaAliasCalculos.AliasGuiaPropia(razaErp));
    }

    // ── Lo que ya cruzaba tiene que seguir cruzando igual ────────────────────
    [Theory]
    [InlineData("Lohmann LSL", "lohmann lsl")]
    [InlineData("Babcock Brown", "babcock brown")]
    [InlineData("Hy Line Brown", "hy line brown")]
    [InlineData("Criolla", "criolla")]
    [InlineData("Azur", "azur")]
    public void AliasGuiaPropia_deja_intacto_el_nombre_comercial(string raza, string esperada)
    {
        Assert.Equal(esperada, RazaGuiaAliasCalculos.AliasGuiaPropia(raza));
    }

    /// <summary>
    /// <c>Lohmann Brown</c> es una línea distinta de <c>Lohmann LSL</c> y todavía no tiene guía
    /// cargada — decisión del usuario del 24-ago-2026. Mapearla a otra raza mostraría datos de un
    /// ave que no es esa, así que se devuelve sin alias.
    /// </summary>
    [Theory]
    [InlineData("LOHMANN BROWN", "lohmann brown")]
    [InlineData("Lohmann Brown", "lohmann brown")]
    public void AliasGuiaPropia_no_inventa_equivalencia_para_lohmann_brown(string raza, string esperada)
    {
        Assert.Equal(esperada, RazaGuiaAliasCalculos.AliasGuiaPropia(raza));
    }

    // ── Bordes ───────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("Ross 308", "ross 308")]
    public void AliasGuiaPropia_raza_desconocida_o_vacia_vuelve_normalizada(string? raza, string esperada)
    {
        Assert.Equal(esperada, RazaGuiaAliasCalculos.AliasGuiaPropia(raza));
    }

    [Fact]
    public void AliasGuiaPropia_es_idempotente()
    {
        var unaVez = RazaGuiaAliasCalculos.AliasGuiaPropia("BABCOK BROWN");
        Assert.Equal(unaVez, RazaGuiaAliasCalculos.AliasGuiaPropia(unaVez));
    }

    [Theory]
    [InlineData("  Lohmann LSL  ", "lohmann lsl")]
    [InlineData("CRIOLLA", "criolla")]
    public void Normalizar_recorta_y_baja_a_minusculas(string raza, string esperada)
    {
        Assert.Equal(esperada, RazaGuiaAliasCalculos.Normalizar(raza));
    }
}
