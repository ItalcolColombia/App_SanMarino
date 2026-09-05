// src/ZooSanMarino.Application/Calculos/AdministracionEmpresasAutorizacionCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Quién puede ESCRIBIR sobre las empresas: crearlas, editarlas, borrarlas, y —sobre todo— decidir
/// qué menús y qué permisos tiene cada una (<c>company_menus</c> / <c>company_permissions</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe.</b> Hasta el 4-sep-2026 <c>CompanyController</c> no declaraba
/// <b>ni un solo <c>[Authorize]</c></b>: lo cubría únicamente la <c>FallbackPolicy</c>
/// (= token válido y nada más). Cualquier sesión autenticada podía, por HTTP directo,
/// <c>POST</c>/<c>PUT</c>/<c>DELETE</c> cualquier empresa y —lo grave—
/// <c>PUT /api/Company/{id}/menus</c> y <c>/permissions</c> sobre <b>cualquier</b> empresa, es decir
/// reasignarse módulos a sí misma o tocar los de otro país. Es el mismo agujero que tenía
/// <c>PermissionController</c> antes del 15-ago-2026, y por el mismo motivo esconder el ítem de menú
/// no alcanzaba: el menú se ve o no se ve, el endpoint responde igual.
/// </para>
/// <para>
/// <b>La regla.</b> Administra empresas quien sea <b>Super Admin</b> (el dato
/// <c>users.is_super_admin</c>, ver <see cref="SuperAdminCalculos"/>) <b>o</b> quien tenga el rol de
/// administrador de la aplicación (<see cref="CatalogoGlobalAutorizacionCalculos.RolesAdminAplicacion"/>).
/// Los dos ejes, no uno: el eje correcto a futuro es el <b>dato</b>, pero el rol se conserva porque
/// es el que hoy sostiene la pantalla y sacarlo dejaría sin acceso a quien la usa.
/// </para>
/// <para>
/// <b>La comparación de roles es EXACTA</b> (heredada de
/// <see cref="CatalogoGlobalAutorizacionCalculos"/>): en la base conviven <c>Admin Panama</c>,
/// <c>Admin Demo</c>, <c>Ecuador Administrador</c>, <c>Santa Reyes Administrador</c> y
/// <c>ADMINISTRADOR DE GRANJA</c> — administradores <b>de su empresa</b>. Un <c>contains</c> les
/// daría a todos la llave para editar las empresas de los demás, que es justo lo que esto viene a
/// cerrar.
/// </para>
/// <para>
/// <b>Las LECTURAS no pasan por acá</b>, a propósito y por dependencias reales:
/// <c>GET /api/Company</c> alimenta el selector de empresa activa,
/// <c>GET /api/Company/global</c> alimenta el <b>filtro del módulo de Tickets</b>, y
/// <c>GET /api/Company/{id}</c> alimenta <c>ActiveCompanyConfigService</c>. Cerrarlas rompería esas
/// tres pantallas para todos.
/// </para>
/// <para>
/// Cálculo PURO: sin EF, sin <c>HttpContext</c>, sin estado. Recibe la marca de super admin y los
/// nombres de rol de la sesión, y responde.
/// </para>
/// </remarks>
public static class AdministracionEmpresasAutorizacionCalculos
{
    /// <summary>
    /// ¿Esta sesión puede escribir sobre las empresas (datos, menús y permisos de empresa)?
    /// </summary>
    /// <param name="esSuperAdmin">
    /// Marca <c>users.is_super_admin</c> de la sesión, tal como la resolvió
    /// <see cref="SuperAdminCalculos.EsSuperAdmin"/>.
    /// </param>
    /// <param name="rolesDeLaSesion">
    /// Nombres de rol del usuario autenticado (claims <c>ClaimTypes.Role</c>).
    /// </param>
    /// <returns>
    /// <c>true</c> si es super admin, o si alguno de sus roles coincide <b>exactamente</b> con un rol
    /// de administrador de aplicación. <b>Fail-closed:</b> sin marca y sin rol reconocido ⇒
    /// <c>false</c>; <c>null</c>, lista vacía o entradas en blanco ⇒ <c>false</c>.
    /// </returns>
    public static bool PuedeAdministrarEmpresas(bool esSuperAdmin, IEnumerable<string?>? rolesDeLaSesion) =>
        esSuperAdmin || CatalogoGlobalAutorizacionCalculos.PuedeEscribirCatalogoGlobal(rolesDeLaSesion);

    /// <summary>
    /// Lee la marca de super admin desde el claim <c>is_super_admin</c> que emite
    /// <c>AuthService</c> al firmar el token (el valor viaja como la cadena <c>"true"</c> /
    /// <c>"false"</c>).
    /// </summary>
    /// <remarks>
    /// Vive acá y no en la policy para que la forma de leer el claim quede cubierta por los tests:
    /// es exactamente la comparación que hace <c>AuthController</c> al poblar
    /// <c>GET /auth/profile</c>. <b>Fail-closed</b>: cualquier valor que no sea <c>"true"</c>
    /// (incluidos <c>null</c> y la cadena vacía) responde <c>false</c>.
    /// </remarks>
    public static bool LeerMarcaSuperAdmin(string? valorDelClaim) =>
        string.Equals(valorDelClaim, "true", StringComparison.OrdinalIgnoreCase);
}
