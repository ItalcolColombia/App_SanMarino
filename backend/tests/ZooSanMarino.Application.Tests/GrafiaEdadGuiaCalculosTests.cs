using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// La semana de transicion aparece dos veces en la guia de esquema completo: <c>"25"</c> (fin del
/// levante) y <c>"25P"</c> (arranque de la produccion, con los acumulados reiniciados). Cada
/// reporte tiene que tomar la suya.
/// </summary>
public class GrafiaEdadGuiaCalculosTests
{
    // ── Reconocer cada forma ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("25P")]
    [InlineData("25p")]
    [InlineData("  25P  ")]
    [InlineData("1P")]
    [InlineData("140P")]
    public void EsFilaDeProduccion_reconoce_el_sufijo_P(string edad)
    {
        Assert.True(GrafiaEdadGuiaCalculos.EsFilaDeProduccion(edad));
        Assert.False(GrafiaEdadGuiaCalculos.EsFilaDeLevante(edad));
    }

    [Theory]
    [InlineData("25")]
    [InlineData("1")]
    [InlineData("  140  ")]
    public void EsFilaDeLevante_reconoce_el_numero_puro(string edad)
    {
        Assert.True(GrafiaEdadGuiaCalculos.EsFilaDeLevante(edad));
        Assert.False(GrafiaEdadGuiaCalculos.EsFilaDeProduccion(edad));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("P")]        // sin numero delante
    [InlineData("SEM 25")]   // texto libre
    [InlineData("25.5")]     // decimal
    [InlineData("25PP")]
    public void Ni_produccion_ni_levante_cuando_la_grafia_no_encaja(string? edad)
    {
        Assert.False(GrafiaEdadGuiaCalculos.EsFilaDeProduccion(edad));
        Assert.False(GrafiaEdadGuiaCalculos.EsFilaDeLevante(edad));
    }

    // ── El desempate ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 🔴 El caso que motivo el calculo: un reporte de PRODUCCION que tomara la fila "25" mostraria
    /// el consumo acumulado del levante entero (11.501 g) en vez del de produccion (847 g).
    /// </summary>
    [Fact]
    public void En_produccion_gana_la_fila_con_P()
    {
        var candidatas = new[] { "25", "25P" };

        var elegida = candidatas
            .OrderBy(e => GrafiaEdadGuiaCalculos.Preferencia(e, paraProduccion: true))
            .First();

        Assert.Equal("25P", elegida);
    }

    [Fact]
    public void En_levante_gana_la_fila_numerica_pura()
    {
        var candidatas = new[] { "25P", "25" };

        var elegida = candidatas
            .OrderBy(e => GrafiaEdadGuiaCalculos.Preferencia(e, paraProduccion: false))
            .First();

        Assert.Equal("25", elegida);
    }

    /// <summary>El orden de entrada no puede cambiar el resultado: el desempate es determinista.</summary>
    [Theory]
    [InlineData(true, "25P")]
    [InlineData(false, "25")]
    public void El_desempate_no_depende_del_orden_de_entrada(bool paraProduccion, string esperada)
    {
        foreach (var candidatas in new[] { new[] { "25", "25P" }, new[] { "25P", "25" } })
        {
            var elegida = candidatas
                .OrderBy(e => GrafiaEdadGuiaCalculos.Preferencia(e, paraProduccion))
                .First();

            Assert.Equal(esperada, elegida);
        }
    }

    /// <summary>
    /// Sin fila con P —el caso de TODAS las semanas menos la de transicion, y de la guia de esquema
    /// simple— la unica candidata gana igual: el desempate no descarta nada.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Con_una_sola_candidata_esa_gana(bool paraProduccion)
    {
        var elegida = new[] { "30" }
            .OrderBy(e => GrafiaEdadGuiaCalculos.Preferencia(e, paraProduccion))
            .First();

        Assert.Equal("30", elegida);
    }

    /// <summary>Una grafia desconocida va al final, nunca antes de una que si se entiende.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void La_grafia_desconocida_queda_ultima(bool paraProduccion)
    {
        var elegida = new[] { "SEM 25", "25", "25P" }
            .OrderBy(e => GrafiaEdadGuiaCalculos.Preferencia(e, paraProduccion))
            .First();

        Assert.NotEqual("SEM 25", elegida);
    }
}
