using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Validación del <c>anio_guia</c> en la ESCRITURA de la guía genética. Plan
/// <c>fase_de_desarrollo/validacion_anio_guia_genetica_plan.md</c> §«Casos de prueba».
///
/// <para>
/// Fijan que la regla de escritura <b>honra</b> el filtro <c>int.TryParse</c> del lado de lectura
/// (<c>GuiaGeneticaService.ObtenerAnosDisponiblesAsync</c>) —un año que pasa acá, sobrevive allá— y
/// que le agrega el único recorte extra que ya existía en el repo: el rango <c>1900–2100</c> del
/// <c>&lt;input type="number" min="1900" max="2100"&gt;</c> del formulario de lote.
/// </para>
/// </summary>
public class AnioGuiaGeneticaCalculosTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Usables
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2026")]
    [InlineData("1900")]   // borde inferior inclusive
    [InlineData("2100")]   // borde superior inclusive
    [InlineData(" 2021 ")] // el Excel arrastra espacios; se recortan antes de parsear
    public void Anio_entero_en_rango_es_usable(string anio)
    {
        Assert.True(AnioGuiaGeneticaCalculos.EsAnioUsable(anio));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // No usables
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("G21")]      // 🔴 el caso real: 143 filas en la tabla ancha que la app nunca pudo usar
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2026 AP")]  // año + sufijo de raza pegado
    [InlineData("2.026")]    // punto de miles / decimal: no es un entero
    [InlineData("2026.0")]   // Excel devuelve así una celda numérica con formato decimal
    [InlineData("20,26")]    // coma de miles / decimal
    [InlineData("1899")]     // fuera de rango por abajo
    [InlineData("2101")]     // fuera de rango por arriba
    [InlineData("-2021")]    // negativo
    public void Anio_no_entero_o_fuera_de_rango_no_es_usable(string? anio)
    {
        Assert.False(AnioGuiaGeneticaCalculos.EsAnioUsable(anio));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 🔴 Invariante de delta cero
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Los 4 valores que hoy existen en la BD (medido el 9-sep-2026 en la copia de prod: tabla ancha
    /// Sanmarino <c>2021/2022/2023/2026</c>, ancha Ecuador <c>2021</c>, ancha Demo <c>2026</c>,
    /// reducida Santa Reyes <c>2026</c>) tienen que seguir siendo usables. Ningún dato vigente puede
    /// quedar del lado inválido por este cambio.
    /// </summary>
    [Theory]
    [InlineData("2021")]
    [InlineData("2022")]
    [InlineData("2023")]
    [InlineData("2026")]
    public void Los_anios_que_ya_viven_en_la_bd_siguen_siendo_usables(string anioEnBd)
    {
        Assert.True(AnioGuiaGeneticaCalculos.EsAnioUsable(anioEnBd));
    }

    /// <summary>
    /// El límite del rango se lee tal cual del formulario de lote: no puede desalinearse del
    /// <c>&lt;input min="1900" max="2100"&gt;</c> sin romper la única-regla.
    /// </summary>
    [Fact]
    public void El_rango_es_el_del_input_del_formulario_de_lote()
    {
        Assert.Equal(1900, AnioGuiaGeneticaCalculos.AnioMinimo);
        Assert.Equal(2100, AnioGuiaGeneticaCalculos.AnioMaximo);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mensaje de rechazo
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void El_mensaje_nombra_el_valor_rechazado_y_el_rango()
    {
        var mensaje = AnioGuiaGeneticaCalculos.MensajeAnioInvalido("G21");

        Assert.Contains("G21", mensaje);
        Assert.Contains("1900", mensaje);
        Assert.Contains("2100", mensaje);
    }

    [Fact]
    public void El_mensaje_recorta_los_espacios_del_valor()
    {
        Assert.Contains("«G21»", AnioGuiaGeneticaCalculos.MensajeAnioInvalido("  G21  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void El_mensaje_para_un_anio_ausente_no_lleva_comillas_vacias(string? anio)
    {
        var mensaje = AnioGuiaGeneticaCalculos.MensajeAnioInvalido(anio);

        Assert.DoesNotContain("«»", mensaje);
        Assert.Contains("obligatorio", mensaje);
        Assert.Contains("1900", mensaje);
        Assert.Contains("2100", mensaje);
    }
}
