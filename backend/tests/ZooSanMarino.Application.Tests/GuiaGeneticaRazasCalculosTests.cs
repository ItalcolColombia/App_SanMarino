using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de <see cref="GuiaGeneticaRazasCalculos"/> — el fix de
/// <c>GuiaGeneticaService.ObtenerRazasCrudoAsync</c> (plan
/// <c>fase_de_desarrollo/guia_genetica_tres_modulos_plan.md</c> §4 F2.4, §5 caso 5).
///
/// <para>
/// El defecto: <c>if (propias.Count &gt; 0) return propias;</c> cortaba a nivel <b>EMPRESA</b>, no
/// de raza. Con 615 filas propias sembradas, una raza cargada en la guía compartida se importaba
/// «OK», se veía en el grid y <b>nunca aparecía en el selector de lotes</b>.
/// </para>
///
/// <para>
/// 🔴 <b>La mitad innegociable de estos tests es la otra:</b> para una empresa SIN guía propia la
/// salida tiene que ser byte a byte la de hoy. Sanmarino, Demo, Ecuador y Panamá están medidas con
/// 0 filas propias, así que TODAS pasan por esa rama.
/// </para>
/// </summary>
public class GuiaGeneticaRazasCalculosTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Delta cero — empresa SIN guía propia
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// La lista compartida vuelve <b>idéntica</b>: mismos elementos, mismo orden y —para que no
    /// quede ninguna duda de que no se tocó— la <b>misma instancia</b>.
    /// </summary>
    [Fact]
    public void Sin_guia_propia_devuelve_la_compartida_sin_tocar()
    {
        var compartidas = new List<string> { "Ross 308", "Cobb 500", "ross 308 ap", "Lohmann Brown" };

        var resultado = GuiaGeneticaRazasCalculos.CombinarRazas(new List<string>(), compartidas);

        Assert.Same(compartidas, resultado);
        Assert.Equal(new[] { "Ross 308", "Cobb 500", "ross 308 ap", "Lohmann Brown" }, resultado);
    }

    /// <summary>
    /// El orden importa: <c>ObtenerRazasDisponiblesAsync</c> ordena después, pero cualquier
    /// reordenamiento acá sería un cambio de comportamiento gratuito para cuatro empresas.
    /// </summary>
    [Fact]
    public void Sin_guia_propia_no_reordena_ni_deduplica()
    {
        // Ojo: la compartida SÍ puede traer dos grafías de la misma raza (no tiene UNIQUE y 644 de
        // sus 1128 filas tienen el código en NULL). Hoy salen las dos, y tienen que seguir saliendo.
        var compartidas = new List<string> { "ROSS 308", "Cobb 500", "Ross 308" };

        var resultado = GuiaGeneticaRazasCalculos.CombinarRazas(null, compartidas);

        Assert.Equal(new[] { "ROSS 308", "Cobb 500", "Ross 308" }, resultado);
    }

    [Fact]
    public void Sin_guia_propia_y_sin_compartida_devuelve_vacio()
    {
        var compartidas = new List<string>();

        Assert.Empty(GuiaGeneticaRazasCalculos.CombinarRazas(new List<string>(), compartidas));
        Assert.Empty(GuiaGeneticaRazasCalculos.CombinarRazas(null, compartidas));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // El fix — empresa CON guía propia
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 🔴 El caso que fallaba en silencio: la empresa tiene sus 5 razas propias y carga una sexta en
    /// la guía compartida. Antes, esa sexta no aparecía nunca.
    /// </summary>
    [Fact]
    public void Con_guia_propia_una_raza_de_la_compartida_ahora_aparece()
    {
        var propias = new List<string> { "Babcock Brown", "Hy Line Brown", "Lohmann LSL", "Criolla", "Azur" };
        var compartidas = new List<string> { "Lohmann Brown" };

        var resultado = GuiaGeneticaRazasCalculos.CombinarRazas(propias, compartidas);

        Assert.Contains("Lohmann Brown", resultado);
        Assert.Equal(6, resultado.Count);
    }

    /// <summary>Las propias van primero y conservan su orden: es la guía que la empresa administra.</summary>
    [Fact]
    public void Las_propias_van_primero_y_en_su_orden()
    {
        var propias = new List<string> { "Babcock Brown", "Criolla" };
        var compartidas = new List<string> { "Lohmann Brown", "Ross 308" };

        Assert.Equal(
            new[] { "Babcock Brown", "Criolla", "Lohmann Brown", "Ross 308" },
            GuiaGeneticaRazasCalculos.CombinarRazas(propias, compartidas));
    }

    /// <summary>
    /// Una raza presente en las dos tablas sale UNA sola vez, con la grafía de la guía propia — que
    /// es la que el resto del sistema usa para cruzar contra ella.
    /// </summary>
    [Fact]
    public void Una_raza_en_ambas_fuentes_no_se_duplica_y_gana_la_propia()
    {
        var propias = new List<string> { "Babcock Brown" };
        var compartidas = new List<string> { "BABCOCK BROWN", "  babcock brown  ", "Ross 308" };

        var resultado = GuiaGeneticaRazasCalculos.CombinarRazas(propias, compartidas);

        Assert.Equal(new[] { "Babcock Brown", "Ross 308" }, resultado);
    }

    [Fact]
    public void Las_propias_repetidas_entre_si_tampoco_se_duplican()
    {
        var propias = new List<string> { "Criolla", "CRIOLLA" };

        Assert.Equal(
            new[] { "Criolla" },
            GuiaGeneticaRazasCalculos.CombinarRazas(propias, new List<string>()));
    }

    [Fact]
    public void Con_guia_propia_y_sin_compartida_devuelve_solo_las_propias()
    {
        var propias = new List<string> { "Babcock Brown", "Criolla" };

        Assert.Equal(
            new[] { "Babcock Brown", "Criolla" },
            GuiaGeneticaRazasCalculos.CombinarRazas(propias, new List<string>()));
    }

    /// <summary>
    /// La combinación no devuelve la instancia recibida cuando sí combinó: si la devolviera, el
    /// llamador podría estar mutando sin querer la lista que le dio EF.
    /// </summary>
    [Fact]
    public void Al_combinar_devuelve_una_lista_nueva()
    {
        var propias = new List<string> { "Criolla" };
        var compartidas = new List<string> { "Ross 308" };

        var resultado = GuiaGeneticaRazasCalculos.CombinarRazas(propias, compartidas);

        Assert.NotSame(propias, resultado);
        Assert.NotSame(compartidas, resultado);
    }
}
