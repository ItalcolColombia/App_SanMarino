using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Perfil de guía genética por empresa (F1 de
/// <c>fase_de_desarrollo/guia_genetica_tres_modulos_plan.md</c>): la señal es
/// <c>companies.guia_genetica_perfil</c>, tipada y nombrada por COMPORTAMIENTO.
/// <para>
/// Lo que estos tests fijan como contrato: el default es neutro (toda empresa que no declare nada
/// sigue en la tabla ancha compartida) y un valor desconocido <b>lanza</b> — nunca cae al default,
/// porque eso mostraría la tabla equivocada sin un solo síntoma visible.
/// </para>
/// </summary>
public class GuiaGeneticaPerfilCalculosTests
{
    // ── Default neutro: sin dato, todo sigue exactamente donde estaba ──────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolver_sin_valor_cae_al_default_sanmarino(string? valor)
    {
        Assert.Equal(GuiaGeneticaPerfilCalculos.Sanmarino, GuiaGeneticaPerfilCalculos.Resolver(valor));
        Assert.Equal("sanmarino", GuiaGeneticaPerfilCalculos.Resolver(valor));
    }

    [Fact]
    public void Default_es_sanmarino()
    {
        // El default de la columna en base y el del cálculo son el MISMO valor: si divergieran, una
        // empresa vieja (columna con el DEFAULT de Postgres) resolvería distinto que una con NULL.
        Assert.Equal("sanmarino", GuiaGeneticaPerfilCalculos.Default);
        Assert.Equal(GuiaGeneticaPerfilCalculos.Sanmarino, GuiaGeneticaPerfilCalculos.Default);
    }

    // ── Los dos valores válidos ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("sanmarino")]
    [InlineData("SANMARINO")]
    [InlineData("  Sanmarino  ")]
    public void Resolver_reconoce_sanmarino(string valor)
    {
        Assert.Equal(GuiaGeneticaPerfilCalculos.Sanmarino, GuiaGeneticaPerfilCalculos.Resolver(valor));
    }

    [Theory]
    [InlineData("reducida")]
    [InlineData("REDUCIDA")]
    [InlineData(" Reducida ")]
    public void Resolver_reconoce_reducida(string valor)
    {
        Assert.Equal(GuiaGeneticaPerfilCalculos.Reducida, GuiaGeneticaPerfilCalculos.Resolver(valor));
    }

    [Fact]
    public void Validos_son_exactamente_los_dos_perfiles()
    {
        Assert.Equal(new[] { "sanmarino", "reducida" }, GuiaGeneticaPerfilCalculos.Validos);
    }

    // ── 🔴 Valor desconocido: lanza, NO cae al default ─────────────────────────────────

    [Theory]
    [InlineData("otro")]
    [InlineData("santa reyes")]
    [InlineData("santa_reyes")]
    [InlineData("ecuador")]
    [InlineData("sanmarino2")]
    [InlineData("reducid")]
    [InlineData("true")]
    public void Resolver_ante_valor_desconocido_lanza(string valor)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => GuiaGeneticaPerfilCalculos.Resolver(valor));

        // El mensaje tiene que decir QUÉ llegó y QUÉ se acepta: este throw sale en un log de prod y
        // sin el valor real no se puede saber qué fila de companies quedó mal.
        Assert.Contains(valor, ex.Message);
        Assert.Contains("sanmarino", ex.Message);
        Assert.Contains("reducida", ex.Message);
    }

    // ── Helpers booleanos ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("sanmarino", false)]
    [InlineData("reducida", true)]
    [InlineData("REDUCIDA", true)]
    public void UsaGuiaReducida(string? valor, bool esperado)
    {
        Assert.Equal(esperado, GuiaGeneticaPerfilCalculos.UsaGuiaReducida(valor));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("sanmarino", true)]
    [InlineData("reducida", false)]
    public void UsaGuiaCompartida(string? valor, bool esperado)
    {
        Assert.Equal(esperado, GuiaGeneticaPerfilCalculos.UsaGuiaCompartida(valor));
    }

    [Fact]
    public void Los_helpers_son_excluyentes_y_tambien_lanzan_ante_lo_desconocido()
    {
        // Ningún perfil puede ser las dos cosas: el guard fail-closed de cada controller se apoya en eso.
        Assert.NotEqual(
            GuiaGeneticaPerfilCalculos.UsaGuiaReducida("reducida"),
            GuiaGeneticaPerfilCalculos.UsaGuiaCompartida("reducida"));

        // Y ninguno de los dos puede "ablandar" el throw devolviendo false en silencio.
        Assert.Throws<ArgumentOutOfRangeException>(() => GuiaGeneticaPerfilCalculos.UsaGuiaReducida("otro"));
        Assert.Throws<ArgumentOutOfRangeException>(() => GuiaGeneticaPerfilCalculos.UsaGuiaCompartida("otro"));
    }

    // ── EsPerfilConocido: la variante que NO lanza (para rechazar con 400, no con 500) ──

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("sanmarino", true)]
    [InlineData(" Reducida ", true)]
    [InlineData("otro", false)]
    [InlineData("santa reyes", false)]
    public void EsPerfilConocido(string? valor, bool esperado)
    {
        Assert.Equal(esperado, GuiaGeneticaPerfilCalculos.EsPerfilConocido(valor));
    }
}
