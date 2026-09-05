// src/ZooSanMarino.Application/Calculos/RolesAutorizacionCalculos.cs
// Regla PURA de quién administra roles, permisos y el catálogo global de menús.
// Sin EF, sin HttpContext, sin estado.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Quién puede administrar <b>roles y permisos</b>, y quién puede leer el <b>catálogo global de
/// menús</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe — el agujero, medido.</b> <c>Program.cs</c> declaraba <c>CanManageRoles</c>,
/// <c>CanManageMenus</c> y <c>CanManageUsers</c> como <c>RequireAuthenticatedUser()</c> —token válido
/// y nada más— con un <c>TODO(seguridad)</c> al lado, y <c>RolesController</c> colgaba de
/// <c>CanManageRoles</c> sus diez endpoints de roles y permisos. Probado en vivo el 5-sep-2026 con el
/// JWT de un usuario real sin ningún permiso de administración:
/// <c>GET /api/Roles</c> → <b>200</b> (todos los roles CON sus permisos),
/// <c>GET /api/Roles/permissions</c> → <b>200</b>, y
/// <c>POST /api/Roles/999999/permissions/assign</c> → <b>404, no 403</b>: la autorización
/// <b>pasó</b> y lo único que lo frenó fue que ese rol no existiera. Con un <c>roleId</c> real
/// habría escrito.
/// </para>
/// <para>
/// <b>Por qué era el agujero de más arriba y no uno más.</b> Los permisos se hornean como claims
/// <c>permission</c> en el token al login (<c>AuthService</c>). Quien puede escribir
/// <c>role_permissions</c> se asigna cualquier key, vuelve a loguearse y se salta <b>todos</b> los
/// demás gates por permiso del sistema —incluidos <c>CargaMasivaPermisoFilter</c> y
/// <c>GestionUsuariosEscrituraFilter</c>—. Mientras esta puerta estuvo abierta, esos gates fueron de
/// papel.
/// </para>
/// <para>
/// <b>La única barrera que había</b> era <c>RoleCompositeService.EnsurePermisosHabilitadosPorEmpresaAsync</c>,
/// que exige que la key esté habilitada en <c>company_permissions</c> de todas las empresas del rol
/// destino. Acota la población atacante a roles de una sola empresa habilitada; no cierra nada.
/// <c>Roles_AddPermissionsAsync</c> no valida quién llama: ni propiedad del rol, ni empresa del
/// llamante, ni super admin.
/// </para>
///
/// <para>
/// 🔴 <b>Por qué la LECTURA de roles tiene una OR con <c>usuarios.gestionar</c>, y no es una
/// concesión.</b> <c>GET /api/Roles</c> no lo consume sólo la pantalla de Roles:
/// <c>RoleService.getAll()</c> alimenta el desplegable de roles del modal de crear/editar usuario y
/// la tabla del listado de usuarios. Medido el 5-sep-2026 sobre la copia de producción, hay
/// <b>3 roles</b> que ven <c>/config/users</c> y NO ven <c>/config/role-management</c>
/// —«Lider implementación - Regional Ecuador», «Consulta», «Usuario pruebas», 4 usuarios—: cerrar la
/// lectura sólo con <c>roles.gestionar</c> les dejaba el dropdown vacío, sin poder asignar rol. Los
/// tres tienen <c>usuarios.gestionar</c> y la key está habilitada en las 5 empresas ⇒ con la OR,
/// <b>0 usuarios</b> pierden algo que hoy usan.
/// </para>
/// <para>
/// <b>Y sin embargo la lectura se cierra</b>, a diferencia de Gestión de Usuarios —donde el listado
/// quedó abierto a propósito—: acá la lectura <b>es</b> el mapa de privilegios del sistema entero
/// (cada rol con sus permisos), o sea el insumo de reconocimiento del ataque que esto viene a cerrar.
/// Blast radius medido: de <b>58</b> sesiones que hoy pueden leerlo a <b>18</b>, y de 58 que pueden
/// escribir <c>role_permissions</c> a <b>15</b>.
/// </para>
/// <para>
/// <c>GET /api/Permission</c> <b>queda abierto</b>, como está desde el 15-ago-2026 y por la razón
/// escrita en <see cref="CatalogoGlobalAutorizacionCalculos"/>: un usuario no admin lo necesita para
/// asignarle permisos a un rol. Es el catálogo de <i>nombres</i> de key, no quién los tiene.
/// </para>
///
/// <para>
/// ⛔ <b>Lo que esto NO toca: <c>CanManageUsers</c>.</b> Esa policy la usan
/// <c>RolesController.MenusForUser</c> y <c>MenuController.GetForUser</c>, ajenos a este módulo
/// —devuelven el menú de OTRO usuario— y hoy siguen siendo «token válido y nada más». Verificado con
/// grep: ningún componente del front los llama. Quedan fuera del alcance de este trabajo, anotados.
/// </para>
///
/// <para>
/// Plan: <c>fase_de_desarrollo/gate_roles_y_menus_plan.md</c>.
/// </para>
/// </remarks>
public static class RolesAutorizacionCalculos
{
    /// <summary>
    /// Permiso que habilita crear, editar y eliminar roles, y asignar/quitar/reemplazar sus permisos.
    /// </summary>
    public const string PermisoGestionarRoles = "roles.gestionar";

    /// <summary>
    /// Permiso que habilita leer el <b>catálogo global</b> de menús (<c>GET /api/Menu/tree</c>,
    /// <c>GET /api/Roles/menus/tree</c>) — el árbol completo de módulos de todos los países.
    /// </summary>
    /// <remarks>
    /// Las ESCRITURAS del árbol ya estaban reservadas al administrador de la aplicación desde el
    /// 15-ago-2026 (<see cref="CatalogoGlobalAutorizacionCalculos"/>); lo que quedaba abierto a
    /// cualquier sesión era la <b>enumeración</b>.
    /// </remarks>
    public const string PermisoGestionarMenus = "menus.gestionar";

    /// <summary>Mensaje del rechazo al escribir roles o permisos.</summary>
    public const string MensajeSinPermisoRoles =
        "No tiene permiso para administrar roles y permisos.";

    /// <summary>
    /// Mensaje del rechazo al LEER roles. Nombra las dos keys que abren la puerta para que quien lo
    /// lea sepa qué pedir.
    /// </summary>
    public const string MensajeSinPermisoLecturaRoles =
        "No tiene permiso para consultar roles y permisos. Se necesita administrar roles " +
        "o administrar usuarios.";

    /// <summary>Mensaje del rechazo al leer el catálogo global de menús.</summary>
    public const string MensajeSinPermisoMenus =
        "No tiene permiso para consultar el catálogo de menús.";

    /// <summary>
    /// ¿Esta sesión puede ESCRIBIR roles y permisos (crear, editar, eliminar y asignar keys)?
    /// </summary>
    /// <param name="esSuperAdmin">
    /// Marca <c>users.is_super_admin</c> de la sesión, tal como la resolvió
    /// <see cref="AdministracionEmpresasAutorizacionCalculos.LeerMarcaSuperAdmin"/>.
    /// </param>
    /// <param name="rolesDeLaSesion">Nombres de rol del usuario (claims <c>ClaimTypes.Role</c>).</param>
    /// <param name="permisos">Keys del claim <c>permission</c> del JWT.</param>
    /// <remarks>
    /// <b>Por qué los ejes super admin / admin de aplicación son necesarios y no un adorno:</b>
    /// <c>AuthService.PermisosEfectivosAsync</c> <b>no</b> le regala permisos al super admin —es
    /// estrictamente <c>role_permissions ∩ company_permissions</c>—, así que una empresa que
    /// deshabilite la key en <c>company_permissions</c> dejaría al único super admin sin ninguna
    /// forma de arreglarlo desde la UI. Es la válvula de seguridad, el mismo criterio de
    /// <see cref="AdministracionEmpresasAutorizacionCalculos"/>.
    ///
    /// <para>
    /// La comparación de roles se delega y es <b>exacta</b> a propósito: en la base conviven
    /// <c>Admin Panama</c>, <c>Admin Demo</c>, <c>Ecuador Administrador</c>,
    /// <c>Santa Reyes Administrador</c> y <c>ADMINISTRADOR DE GRANJA</c>, que son administradores
    /// <b>de su empresa</b>. Un <c>contains</c> les daría a todos la llave para repartir permisos en
    /// los roles de los demás países — justo lo que esto cierra.
    /// </para>
    /// </remarks>
    public static bool PuedeGestionarRoles(
        bool esSuperAdmin,
        IEnumerable<string?>? rolesDeLaSesion,
        IEnumerable<string?>? permisos) =>
        AdministracionEmpresasAutorizacionCalculos.PuedeAdministrarEmpresas(esSuperAdmin, rolesDeLaSesion)
        || Tiene(permisos, PermisoGestionarRoles);

    /// <summary>
    /// ¿Esta sesión puede LEER roles y su mapa de permisos?
    /// </summary>
    /// <remarks>
    /// Quien administra roles, sí. Y también quien administra usuarios
    /// (<see cref="GestionUsuariosAutorizacionCalculos.PermisoGestionar"/>), porque necesita la lista
    /// de roles para asignárselos a un usuario — ver la nota de la clase: sin esta OR, 4 usuarios
    /// medidos se quedan con el dropdown de roles vacío.
    /// </remarks>
    public static bool PuedeLeerRoles(
        bool esSuperAdmin,
        IEnumerable<string?>? rolesDeLaSesion,
        IEnumerable<string?>? permisos) =>
        PuedeGestionarRoles(esSuperAdmin, rolesDeLaSesion, permisos)
        || Tiene(permisos, GestionUsuariosAutorizacionCalculos.PermisoGestionar);

    /// <summary>
    /// ¿Esta sesión puede leer el catálogo GLOBAL de menús?
    /// </summary>
    /// <remarks>
    /// Lo consumen dos pantallas: «Roles y permisos» (<c>/config/role-management</c>) y «Empresas»
    /// (<c>/config/companies</c>). El sidebar NO pasa por acá: usa <c>menus/me</c>, que devuelve el
    /// menú del propio usuario y queda abierto.
    ///
    /// <para>
    /// Es una key independiente de <see cref="PermisoGestionarRoles"/> a propósito: administrar los
    /// roles de una empresa no obliga a ver el árbol de módulos de todos los países.
    /// </para>
    /// </remarks>
    public static bool PuedeLeerCatalogoMenus(
        bool esSuperAdmin,
        IEnumerable<string?>? rolesDeLaSesion,
        IEnumerable<string?>? permisos) =>
        AdministracionEmpresasAutorizacionCalculos.PuedeAdministrarEmpresas(esSuperAdmin, rolesDeLaSesion)
        || Tiene(permisos, PermisoGestionarMenus);

    /// <summary>
    /// ¿Es una operación de sólo LECTURA? Se expone para que el criterio viva en un solo lugar,
    /// igual que en <see cref="GestionUsuariosAutorizacionCalculos.EsLectura"/>.
    /// </summary>
    public static bool EsLectura(string? metodoHttp) =>
        string.Equals((metodoHttp ?? string.Empty).Trim(), "GET", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// ¿Está la key entre los permisos? <b>Fail-closed</b>: <c>null</c> ⇒ no. Comparación
    /// <b>ordinal</b>, igual que el resto de los gates por permiso del repo: las keys son
    /// identificadores, no texto para humanos, y <c>Roles.Gestionar</c> no es <c>roles.gestionar</c>.
    /// </summary>
    private static bool Tiene(IEnumerable<string?>? permisos, string key)
    {
        if (permisos is null) return false;

        foreach (var permiso in permisos)
        {
            if (string.IsNullOrWhiteSpace(permiso)) continue;
            if (string.Equals(permiso, key, StringComparison.Ordinal)) return true;
        }

        return false;
    }
}
