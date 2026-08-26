using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de las dos guardas de escritura de guía genética (plan
/// <c>fase_de_desarrollo/guia_genetica_tres_modulos_plan.md</c> §4 F2.3, §5 caso 6).
///
/// <para>
/// Lo que había antes: <b>nada</b>. Ninguno de los tres controllers de guía miraba un permiso, y el
/// de la tabla compartida además borra en duro. Cualquier sesión válida podía reescribir la guía
/// genética de su empresa — el insumo de todos los indicadores técnicos.
/// </para>
/// </summary>
public class GuiaGeneticaEscrituraAutorizacionCalculosTests
{
    private static readonly string[] ConPermiso = { "editar_registro", "guia_genetica.gestionar", "tickets.crear" };
    private static readonly string[] SinPermiso = { "editar_registro", "eliminar_registro", "tickets.crear" };

    // ─────────────────────────────────────────────────────────────────────────
    // Puerta 1 — quién
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Con_la_key_puede_gestionar()
        => Assert.True(GuiaGeneticaEscrituraAutorizacionCalculos.PuedeGestionar(ConPermiso));

    [Fact]
    public void Sin_la_key_no_puede_gestionar()
        => Assert.False(GuiaGeneticaEscrituraAutorizacionCalculos.PuedeGestionar(SinPermiso));

    /// <summary>
    /// <c>editar_registro</c> es transversal (habilita seguimiento diario, movimientos y ventas): si
    /// alcanzara, este permiso no separaría nada.
    /// </summary>
    [Fact]
    public void Editar_registro_no_habilita_la_guia_genetica()
        => Assert.False(GuiaGeneticaEscrituraAutorizacionCalculos.PuedeGestionar(new[] { "editar_registro" }));

    [Fact]
    public void Fail_closed_sin_permisos()
    {
        Assert.False(GuiaGeneticaEscrituraAutorizacionCalculos.PuedeGestionar(null));
        Assert.False(GuiaGeneticaEscrituraAutorizacionCalculos.PuedeGestionar(Array.Empty<string>()));
    }

    /// <summary>
    /// La comparación es ordinal, igual que el resto de los gates por permiso del repo: una key con
    /// otra caja NO es la key.
    /// </summary>
    [Fact]
    public void La_comparacion_de_la_key_es_ordinal()
        => Assert.False(GuiaGeneticaEscrituraAutorizacionCalculos.PuedeGestionar(new[] { "GUIA_GENETICA.GESTIONAR" }));

    /// <summary>
    /// La key exacta que la migración de F4 tiene que sembrar. Si acá se renombra y allá no, el
    /// módulo queda inutilizable sin un solo error visible.
    /// </summary>
    [Fact]
    public void La_key_es_la_que_siembra_la_migracion()
        => Assert.Equal("guia_genetica.gestionar", GuiaGeneticaEscrituraAutorizacionCalculos.PermisoGestionar);

    [Theory]
    [InlineData("GET", true)]
    [InlineData("get", true)]
    [InlineData(" GET ", true)]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("DELETE", false)]
    [InlineData("PATCH", false)]
    [InlineData(null, false)]
    public void Solo_el_GET_es_lectura(string? metodo, bool esperado)
        => Assert.Equal(esperado, GuiaGeneticaEscrituraAutorizacionCalculos.EsLectura(metodo));

    // ─────────────────────────────────────────────────────────────────────────
    // Puerta 2 — dónde (fail-closed en los DOS sentidos)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Perfil_reducida_escribe_la_tabla_reducida()
        => Assert.True(GuiaGeneticaEscrituraAutorizacionCalculos.PuedeEscribirEnPerfil(
            GuiaGeneticaPerfilCalculos.Reducida, GuiaGeneticaPerfilCalculos.Reducida));

    [Fact]
    public void Perfil_sanmarino_escribe_la_tabla_compartida()
        => Assert.True(GuiaGeneticaEscrituraAutorizacionCalculos.PuedeEscribirEnPerfil(
            GuiaGeneticaPerfilCalculos.Sanmarino, GuiaGeneticaPerfilCalculos.Sanmarino));

    /// <summary>🔴 Perfil <c>reducida</c> escribiendo en la compartida ⇒ rechazo.</summary>
    [Fact]
    public void Perfil_reducida_NO_escribe_la_tabla_compartida()
        => Assert.False(GuiaGeneticaEscrituraAutorizacionCalculos.PuedeEscribirEnPerfil(
            GuiaGeneticaPerfilCalculos.Reducida, GuiaGeneticaPerfilCalculos.Sanmarino));

    /// <summary>🔴 Y al revés: perfil <c>sanmarino</c> escribiendo en la reducida ⇒ rechazo.</summary>
    [Fact]
    public void Perfil_sanmarino_NO_escribe_la_tabla_reducida()
        => Assert.False(GuiaGeneticaEscrituraAutorizacionCalculos.PuedeEscribirEnPerfil(
            GuiaGeneticaPerfilCalculos.Sanmarino, GuiaGeneticaPerfilCalculos.Reducida));

    /// <summary>
    /// Empresa sin perfil declarado (columna vacía, empresa vieja) ⇒ default <c>sanmarino</c>:
    /// escribe la compartida como siempre —esto es lo que mantiene el delta cero de las cuatro
    /// empresas que hoy escriben ahí— y la reducida le queda cerrada.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_perfil_declarado_escribe_la_compartida_y_no_la_reducida(string? perfil)
    {
        Assert.True(GuiaGeneticaEscrituraAutorizacionCalculos.PuedeEscribirEnPerfil(
            perfil, GuiaGeneticaPerfilCalculos.Sanmarino));

        Assert.False(GuiaGeneticaEscrituraAutorizacionCalculos.PuedeEscribirEnPerfil(
            perfil, GuiaGeneticaPerfilCalculos.Reducida));
    }

    /// <summary>El perfil tolera espaciado y caja, igual que <c>GuiaGeneticaPerfilCalculos.Resolver</c>.</summary>
    [Theory]
    [InlineData(" REDUCIDA ")]
    [InlineData("Reducida")]
    public void El_perfil_se_normaliza_antes_de_comparar(string perfil)
        => Assert.True(GuiaGeneticaEscrituraAutorizacionCalculos.PuedeEscribirEnPerfil(
            perfil, GuiaGeneticaPerfilCalculos.Reducida));

    /// <summary>
    /// Un perfil desconocido <b>lanza</b>, no cae al default: dejar escribir en la tabla equivocada
    /// en silencio es peor que fallar (decisión de F1, se hereda tal cual).
    /// </summary>
    [Fact]
    public void Un_perfil_desconocido_lanza_en_vez_de_caer_al_default()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            GuiaGeneticaEscrituraAutorizacionCalculos.PuedeEscribirEnPerfil(
                "santa reyes", GuiaGeneticaPerfilCalculos.Reducida));

    // ─────────────────────────────────────────────────────────────────────────
    // Mensajes
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El mensaje manda al módulo correcto. Un «403» pelado en esta pantalla es indistinguible de
    /// «me falta un permiso» y hace pedirle al administrador algo que no va a servir.
    /// </summary>
    [Fact]
    public void El_mensaje_nombra_el_modulo_al_que_hay_que_ir()
    {
        Assert.Contains(
            "Guía Genética Sanmarino",
            GuiaGeneticaEscrituraAutorizacionCalculos.MensajePerfilIncorrecto(GuiaGeneticaPerfilCalculos.Reducida));

        Assert.Contains(
            "Guía Genética Santa Reyes",
            GuiaGeneticaEscrituraAutorizacionCalculos.MensajePerfilIncorrecto(GuiaGeneticaPerfilCalculos.Sanmarino));
    }

    /// <summary>El rechazo por permiso dice qué SÍ se puede hacer, para no leerse como «perdí el módulo».</summary>
    [Fact]
    public void El_mensaje_sin_permiso_aclara_que_la_lectura_sigue_abierta()
        => Assert.Contains("consultarla", GuiaGeneticaEscrituraAutorizacionCalculos.MensajeSinPermiso);
}
