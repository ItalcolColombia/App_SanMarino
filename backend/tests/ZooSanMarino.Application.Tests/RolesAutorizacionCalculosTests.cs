using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de <see cref="RolesAutorizacionCalculos"/> — el gate que cierra la escalada de
/// privilegios de <c>CanManageRoles</c> (plan
/// <c>fase_de_desarrollo/gate_roles_y_menus_plan.md</c>).
///
/// <para>
/// Estas pruebas son el gate de CI del cambio. Las dos que más importan y por qué:
/// <list type="bullet">
///   <item><description>
///     <see cref="SoloUsuariosGestionar_Lee_PeroNoEscribe"/> — si se cae, 4 usuarios medidos se
///     quedan sin el desplegable de roles del modal de usuarios (lockout).
///   </description></item>
///   <item><description>
///     <see cref="AdministradoresDeEmpresa_NoSonAdminDeAplicacion"/> — si se cae, cualquier
///     «Admin Panama» / «Ecuador Administrador» reparte permisos en los roles de los otros países.
///   </description></item>
/// </list>
/// </para>
/// </summary>
public class RolesAutorizacionCalculosTests
{
    private const string Roles = RolesAutorizacionCalculos.PermisoGestionarRoles;   // roles.gestionar
    private const string Menus = RolesAutorizacionCalculos.PermisoGestionarMenus;   // menus.gestionar
    private const string Usuarios = GestionUsuariosAutorizacionCalculos.PermisoGestionar;

    private static readonly string[] SinRoles = Array.Empty<string>();

    // ─────────────────────────────────────────────────────────────────────────
    // Fail-closed. El default del sistema se INVIERTE con este cambio (antes podían todos), así que
    // «no me consta» tiene que ser «no».
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SinNada_NoPuedeNada()
    {
        Assert.False(RolesAutorizacionCalculos.PuedeGestionarRoles(false, SinRoles, Array.Empty<string>()));
        Assert.False(RolesAutorizacionCalculos.PuedeLeerRoles(false, SinRoles, Array.Empty<string>()));
        Assert.False(RolesAutorizacionCalculos.PuedeLeerCatalogoMenus(false, SinRoles, Array.Empty<string>()));
    }

    [Fact]
    public void Nulls_NoPuedenNada()
    {
        Assert.False(RolesAutorizacionCalculos.PuedeGestionarRoles(false, null, null));
        Assert.False(RolesAutorizacionCalculos.PuedeLeerRoles(false, null, null));
        Assert.False(RolesAutorizacionCalculos.PuedeLeerCatalogoMenus(false, null, null));
    }

    [Fact]
    public void EntradasEnBlanco_NoPuedenNada()
    {
        var basura = new string?[] { null, "", "   " };

        Assert.False(RolesAutorizacionCalculos.PuedeGestionarRoles(false, basura, basura));
        Assert.False(RolesAutorizacionCalculos.PuedeLeerRoles(false, basura, basura));
        Assert.False(RolesAutorizacionCalculos.PuedeLeerCatalogoMenus(false, basura, basura));
    }

    /// <summary>
    /// El caso que se probó en vivo el 5-sep-2026: ALEX ALTAMIRANO, 14 permisos efectivos y ninguno
    /// de gestión. Antes del cambio, <c>POST …/permissions/assign</c> le devolvía 404 (o sea: la
    /// autorización pasaba).
    /// </summary>
    [Fact]
    public void UsuarioOperativo_ConPermisosDeSuModulo_NoAdministraRoles()
    {
        var permisos = new[]
        {
            "seguimiento_levante.validar", "editar_registro", "vacunacion.cronograma.ver",
            "carga_masiva_postura", "tickets.crear"
        };

        Assert.False(RolesAutorizacionCalculos.PuedeGestionarRoles(false, new[] { "Operario" }, permisos));
        Assert.False(RolesAutorizacionCalculos.PuedeLeerRoles(false, new[] { "Operario" }, permisos));
        Assert.False(RolesAutorizacionCalculos.PuedeLeerCatalogoMenus(false, new[] { "Operario" }, permisos));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Las keys, y su independencia entre sí.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ConRolesGestionar_AdministraYLee()
    {
        Assert.True(RolesAutorizacionCalculos.PuedeGestionarRoles(false, SinRoles, new[] { Roles }));
        Assert.True(RolesAutorizacionCalculos.PuedeLeerRoles(false, SinRoles, new[] { Roles }));
    }

    /// <summary>
    /// Las dos keys son independientes a propósito: administrar los roles de una empresa no obliga a
    /// poder enumerar el árbol de módulos de todos los países.
    /// </summary>
    [Fact]
    public void LasDosKeys_SonIndependientes()
    {
        Assert.False(RolesAutorizacionCalculos.PuedeLeerCatalogoMenus(false, SinRoles, new[] { Roles }));
        Assert.False(RolesAutorizacionCalculos.PuedeGestionarRoles(false, SinRoles, new[] { Menus }));
        Assert.True(RolesAutorizacionCalculos.PuedeLeerCatalogoMenus(false, SinRoles, new[] { Menus }));
    }

    /// <summary>
    /// 🔴 ANTI-LOCKOUT. <c>GET /api/Roles</c> alimenta el desplegable de roles del modal de
    /// crear/editar usuario y la tabla del listado. Medido sobre la copia de producción: 3 roles
    /// (4 usuarios) ven <c>/config/users</c> y NO <c>/config/role-management</c>, y los tres tienen
    /// <c>usuarios.gestionar</c>. Sin esta OR se quedan con el dropdown vacío.
    /// Escribir, en cambio, sigue estando cerrado para ellos.
    /// </summary>
    [Fact]
    public void SoloUsuariosGestionar_Lee_PeroNoEscribe()
    {
        var permisos = new[] { Usuarios };

        Assert.True(RolesAutorizacionCalculos.PuedeLeerRoles(false, SinRoles, permisos));
        Assert.False(RolesAutorizacionCalculos.PuedeGestionarRoles(false, SinRoles, permisos));
        Assert.False(RolesAutorizacionCalculos.PuedeLeerCatalogoMenus(false, SinRoles, permisos));
    }

    /// <summary>
    /// Comparación ORDINAL: las keys son identificadores, no texto para humanos.
    /// </summary>
    [Theory]
    [InlineData("Roles.Gestionar")]
    [InlineData("ROLES.GESTIONAR")]
    [InlineData("roles_gestionar")]
    [InlineData("roles.gestionar ")]
    public void KeyMalEscrita_NoConcede(string key)
    {
        Assert.False(RolesAutorizacionCalculos.PuedeGestionarRoles(false, SinRoles, new[] { key }));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // La válvula: super admin y rol de administrador de la aplicación.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>AuthService.PermisosEfectivosAsync</c> no le regala permisos al super admin: es
    /// <c>role_permissions ∩ company_permissions</c>. Sin esta válvula, deshabilitar la key en
    /// <c>company_permissions</c> dejaría al único super admin sin forma de arreglarlo desde la UI.
    /// </summary>
    [Fact]
    public void SuperAdmin_SinNingunaKey_Puede()
    {
        Assert.True(RolesAutorizacionCalculos.PuedeGestionarRoles(true, SinRoles, Array.Empty<string>()));
        Assert.True(RolesAutorizacionCalculos.PuedeLeerRoles(true, SinRoles, Array.Empty<string>()));
        Assert.True(RolesAutorizacionCalculos.PuedeLeerCatalogoMenus(true, SinRoles, Array.Empty<string>()));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("  Admin  ")]
    [InlineData("Administrador")]
    public void RolAdminDeAplicacion_Puede(string rol)
    {
        Assert.True(RolesAutorizacionCalculos.PuedeGestionarRoles(false, new[] { rol }, Array.Empty<string>()));
        Assert.True(RolesAutorizacionCalculos.PuedeLeerRoles(false, new[] { rol }, Array.Empty<string>()));
        Assert.True(RolesAutorizacionCalculos.PuedeLeerCatalogoMenus(false, new[] { rol }, Array.Empty<string>()));
    }

    /// <summary>
    /// 🔴 Estos son administradores <b>de su empresa</b>, no de la aplicación. Todos existen en la
    /// base. Un <c>contains</c> en vez de la comparación exacta les daría la llave para repartir
    /// permisos en los roles de los otros países — el agujero que este trabajo cierra, reabierto por
    /// la puerta de al lado.
    /// </summary>
    [Theory]
    [InlineData("Admin Panama")]
    [InlineData("Admin Demo")]
    [InlineData("Ecuador Administrador")]
    [InlineData("Santa Reyes Administrador")]
    [InlineData("Santa Reyes Implementador")]
    [InlineData("ADMINISTRADOR DE GRANJA")]
    [InlineData("Sistemas sanmarino")]
    [InlineData("Lider Funcional")]
    public void AdministradoresDeEmpresa_NoSonAdminDeAplicacion(string rol)
    {
        Assert.False(RolesAutorizacionCalculos.PuedeGestionarRoles(false, new[] { rol }, Array.Empty<string>()));
        Assert.False(RolesAutorizacionCalculos.PuedeLeerRoles(false, new[] { rol }, Array.Empty<string>()));
        Assert.False(RolesAutorizacionCalculos.PuedeLeerCatalogoMenus(false, new[] { rol }, Array.Empty<string>()));
    }

    /// <summary>
    /// Y con la key sembrada por la migración sí pueden: es así como los 11 roles que hoy ven el
    /// módulo lo conservan.
    /// </summary>
    [Fact]
    public void AdministradorDeEmpresa_ConLaKey_Puede()
    {
        var roles = new[] { "Admin Panama" };

        Assert.True(RolesAutorizacionCalculos.PuedeGestionarRoles(false, roles, new[] { Roles }));
        Assert.True(RolesAutorizacionCalculos.PuedeLeerCatalogoMenus(false, roles, new[] { Menus }));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lectura vs escritura.
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GET", true)]
    [InlineData("get", true)]
    [InlineData("  GET  ", true)]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("DELETE", false)]
    [InlineData("PATCH", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EsLectura_SoloGet(string? metodo, bool esperado)
    {
        Assert.Equal(esperado, RolesAutorizacionCalculos.EsLectura(metodo));
    }

    /// <summary>
    /// Quien administra roles también lee: la lectura nunca es más estricta que la escritura.
    /// </summary>
    [Theory]
    [InlineData(true, new string[0], new string[0])]
    [InlineData(false, new[] { "Admin" }, new string[0])]
    [InlineData(false, new string[0], new[] { Roles })]
    public void QuienEscribe_TambienLee(bool esSuperAdmin, string[] roles, string[] permisos)
    {
        Assert.True(RolesAutorizacionCalculos.PuedeGestionarRoles(esSuperAdmin, roles, permisos));
        Assert.True(RolesAutorizacionCalculos.PuedeLeerRoles(esSuperAdmin, roles, permisos));
    }
}
