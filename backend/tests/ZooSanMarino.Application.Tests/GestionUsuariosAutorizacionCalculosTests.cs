using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests del permiso <c>usuarios.gestionar</c>
/// (`fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md` §3).
///
/// <para>
/// Lo que había antes: nada. Cualquier sesión válida podía crear, editar y borrar usuarios,
/// resetear contraseñas y asignar granjas — incluido el toggle de «administrador de granja», que es
/// una escalada de privilegios. Estas pruebas fijan las dos mitades de la regla: que la escritura
/// exija la key, y que la LECTURA siga abierta (que es lo que se pidió, y lo que un endurecimiento
/// entusiasta rompería sin querer).
/// </para>
/// </summary>
public class GestionUsuariosAutorizacionCalculosTests
{
    private static readonly string[] ConPermiso = { "editar_registro", "usuarios.gestionar", "tickets.crear" };
    private static readonly string[] SinPermiso = { "editar_registro", "eliminar_registro", "tickets.crear" };

    [Fact]
    public void Con_la_key_puede_gestionar()
    {
        Assert.True(GestionUsuariosAutorizacionCalculos.PuedeGestionar(ConPermiso));
    }

    [Fact]
    public void Sin_la_key_no_puede_gestionar()
    {
        Assert.False(GestionUsuariosAutorizacionCalculos.PuedeGestionar(SinPermiso));
    }

    /// <summary>
    /// `editar_registro` es transversal y NO alcanza: si alcanzara, este permiso no separaría nada —
    /// casi todos los roles operativos lo tienen.
    /// </summary>
    [Fact]
    public void Editar_registro_no_habilita_la_gestion_de_usuarios()
    {
        Assert.False(GestionUsuariosAutorizacionCalculos.PuedeGestionar(new[] { "editar_registro" }));
    }

    [Fact]
    public void Fail_closed_sin_permisos()
    {
        Assert.False(GestionUsuariosAutorizacionCalculos.PuedeGestionar(null));
        Assert.False(GestionUsuariosAutorizacionCalculos.PuedeGestionar(Array.Empty<string>()));
    }

    [Fact]
    public void La_comparacion_es_ordinal()
    {
        Assert.False(GestionUsuariosAutorizacionCalculos.PuedeGestionar(new[] { "Usuarios.Gestionar" }));
        Assert.False(GestionUsuariosAutorizacionCalculos.PuedeGestionar(new[] { "usuarios.gestionar " }));
    }

    // ─── La mitad que se rompe sin querer ──────────────────────────────────────

    /// <summary>
    /// El pedido es explícito: sin el permiso se ve el listado y el detalle. Si un endurecimiento
    /// futuro tratara los GET como escritura, el módulo desaparecería para todo el mundo salvo
    /// quienes administran — y nadie lo notaría hasta que un usuario lo reporte.
    /// </summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("get")]
    [InlineData("  Get  ")]
    public void El_GET_es_lectura_y_queda_abierto(string metodo)
    {
        Assert.True(GestionUsuariosAutorizacionCalculos.EsLectura(metodo));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("")]
    [InlineData(null)]
    public void Todo_lo_demas_es_escritura(string? metodo)
    {
        Assert.False(GestionUsuariosAutorizacionCalculos.EsLectura(metodo));
    }

    /// <summary>
    /// La key es exactamente la que siembra la migración `20260825130000_SeedPermisoUsuariosGestionar`.
    /// Cambiarla en el código sin cambiarla en la BD deja el gate cerrado para todos, en silencio.
    /// </summary>
    [Fact]
    public void La_key_es_la_que_siembra_la_migracion()
    {
        Assert.Equal("usuarios.gestionar", GestionUsuariosAutorizacionCalculos.PermisoGestionar);
    }
}
