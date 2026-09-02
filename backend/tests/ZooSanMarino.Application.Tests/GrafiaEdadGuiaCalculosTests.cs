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

    // ── Que entra en la serie de PRODUCCION ─────────────────────────────────────────────────────

    /// <summary>
    /// 🔴 El caso que motivo `EsSerieDeProduccion`: el corte por numero (>= 26) descartaba `25P`,
    /// que parsea a 25 y es la fila que ABRE la produccion. La semana de transicion se quedaba sin
    /// consumo, peso ni mortalidad standard.
    /// </summary>
    [Fact]
    public void La_fila_25P_entra_en_la_serie_de_produccion_pese_a_parsear_25()
    {
        Assert.True(GrafiaEdadGuiaCalculos.EsSerieDeProduccion("25P", 25));
    }

    /// <summary>La fila de LEVANTE de la misma semana sigue fuera: es lo que el corte protege.</summary>
    [Fact]
    public void La_fila_25_de_levante_NO_entra_en_la_serie_de_produccion()
    {
        Assert.False(GrafiaEdadGuiaCalculos.EsSerieDeProduccion("25", 25));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("18", 18)]
    [InlineData("24", 24)]
    public void Las_semanas_de_levante_quedan_fuera(string grafia, int edad)
    {
        Assert.False(GrafiaEdadGuiaCalculos.EsSerieDeProduccion(grafia, edad));
    }

    [Theory]
    [InlineData("26", 26)]
    [InlineData("53", 53)]
    [InlineData("140", 140)]
    public void Desde_el_corte_entran_todas(string grafia, int edad)
    {
        Assert.True(GrafiaEdadGuiaCalculos.EsSerieDeProduccion(grafia, edad));
    }

    // ── El corte depende de la GUIA de la empresa (`companies.semana_inicio_produccion_guia`) ──

    /// <summary>
    /// 🔴 Guia de ESQUEMA SIMPLE (corte 18): arranca directamente en produccion. Medido: su primera
    /// edad es la 18 y ya trae produccion ahi — 7,70 % en Hy Line Brown, subiendo a 96,60 % en la
    /// semana 25. Con el corte fijo en 26 se perdian esas 8 semanas, que son la curva de arranque
    /// de la postura.
    /// </summary>
    [Theory]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(140)]
    public void Con_corte_18_toda_la_guia_de_esquema_simple_es_produccion(int edad)
    {
        Assert.True(GrafiaEdadGuiaCalculos.EsSerieDeProduccion(edad.ToString(), edad, desdeSemana: 18));
    }

    /// <summary>
    /// Guia de ESQUEMA COMPLETO (corte 26, el default): cubre levante + postura, asi que las
    /// semanas de levante tienen que quedar fuera. Es lo que el corte protege.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(18)]
    [InlineData(24)]
    [InlineData(25)]
    public void Con_el_corte_por_defecto_el_levante_queda_fuera(int edad)
    {
        Assert.False(GrafiaEdadGuiaCalculos.EsSerieDeProduccion(edad.ToString(), edad));
    }

    /// <summary>
    /// El default del calculo es el mismo numero que estaba escrito a mano en el service antes de
    /// que el corte fuera un parametro: quien no declare nada se comporta igual que siempre.
    /// </summary>
    [Fact]
    public void El_default_es_26_y_coincide_con_el_corte_historico()
    {
        Assert.Equal(26, GrafiaEdadGuiaCalculos.PrimeraSemanaProduccion);

        for (var edad = 1; edad <= 140; edad++)
        {
            Assert.Equal(
                GrafiaEdadGuiaCalculos.EsSerieDeProduccion(edad.ToString(), edad),
                GrafiaEdadGuiaCalculos.EsSerieDeProduccion(edad.ToString(), edad, desdeSemana: 26));
        }
    }

    /// <summary>
    /// La grafia de transicion entra <b>cualquiera sea el corte</b>: no depende de el, sino de que
    /// esa fila abre la serie de produccion por definicion.
    /// </summary>
    [Theory]
    [InlineData(18)]
    [InlineData(26)]
    [InlineData(99)]
    public void La_fila_con_P_entra_con_cualquier_corte(int desdeSemana)
    {
        Assert.True(GrafiaEdadGuiaCalculos.EsSerieDeProduccion("25P", 25, desdeSemana));
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
