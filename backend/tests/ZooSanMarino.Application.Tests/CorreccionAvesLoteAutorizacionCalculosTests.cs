using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests del permiso <c>lote.corregir_aves</c>
/// (`fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md` §2).
///
/// <para>
/// La regla tiene dos mitades y las dos importan: que corregir las aves exija la key nueva, y que
/// el RESTO del formulario del lote siga abierto. Confundir la segunda convierte este permiso en un
/// segundo <c>editar_registro</c> — exactamente el problema que vino a resolver.
/// </para>
/// </summary>
public class CorreccionAvesLoteAutorizacionCalculosTests
{
    private static readonly string[] ConPermiso = { "editar_registro", "lote.corregir_aves" };
    private static readonly string[] SoloEditarRegistro = { "editar_registro", "eliminar_registro" };

    [Fact]
    public void Con_la_key_puede_corregir_las_aves()
    {
        Assert.True(CorreccionAvesLoteAutorizacionCalculos.PuedeAplicar(true, ConPermiso));
    }

    [Fact]
    public void Sin_la_key_no_puede_corregir_las_aves()
    {
        Assert.False(CorreccionAvesLoteAutorizacionCalculos.PuedeAplicar(true, SoloEditarRegistro));
    }

    /// <summary>
    /// El gate mira el DELTA, no el verbo. Un PUT que solo cambia el técnico, la regional o el
    /// código ERP no mueve aves y no puede pedir este permiso: si lo pidiera, le rompería la
    /// pantalla a todo el que hoy edita un lote sin tocar el encasetamiento.
    /// </summary>
    [Fact]
    public void Un_PUT_que_no_mueve_aves_no_pide_el_permiso()
    {
        Assert.True(CorreccionAvesLoteAutorizacionCalculos.PuedeAplicar(false, SoloEditarRegistro));
        Assert.True(CorreccionAvesLoteAutorizacionCalculos.PuedeAplicar(false, null));
        Assert.True(CorreccionAvesLoteAutorizacionCalculos.PuedeAplicar(false, Array.Empty<string>()));
    }

    /// <summary>
    /// `editar_registro` no alcanza: es transversal (habilita también seguimiento diario,
    /// movimientos y ventas de engorde e inventario) y darlo para esto sería volver al punto de
    /// partida.
    /// </summary>
    [Fact]
    public void Editar_registro_no_alcanza_para_corregir_aves()
    {
        Assert.False(CorreccionAvesLoteAutorizacionCalculos.TienePermiso(new[] { "editar_registro" }));
    }

    [Fact]
    public void Fail_closed_sin_permisos()
    {
        Assert.False(CorreccionAvesLoteAutorizacionCalculos.PuedeAplicar(true, null));
        Assert.False(CorreccionAvesLoteAutorizacionCalculos.PuedeAplicar(true, Array.Empty<string>()));
    }

    [Fact]
    public void La_comparacion_es_ordinal()
    {
        Assert.False(CorreccionAvesLoteAutorizacionCalculos.TienePermiso(new[] { "Lote.Corregir_Aves" }));
    }

    /// <summary>
    /// La key es exactamente la que siembra `20260825140000_SeedPermisoLoteCorregirAves`. Si el
    /// código y la BD se separan, el gate queda cerrado para todos sin un solo error visible.
    /// </summary>
    [Fact]
    public void La_key_es_la_que_siembra_la_migracion()
    {
        Assert.Equal("lote.corregir_aves", CorreccionAvesLoteAutorizacionCalculos.PermisoCorregirAves);
    }
}
