// src/ZooSanMarino.API/Infrastructure/RolesGestionFilter.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.API.Infrastructure;

/// <summary>
/// Marca un endpoint como <b>gateado por otra política</b>, exceptuándolo de
/// <see cref="RolesGestionFilterAttribute"/>.
///
/// <para>Sus tres usos legítimos en <c>RolesController</c>, todos verificados:</para>
/// <list type="bullet">
///   <item><description>
///     <c>GET menus/me</c> — el menú del <b>propio</b> usuario, que alimenta el sidebar de toda la
///     aplicación. Cerrarlo dejaría a todo el mundo sin menú.
///   </description></item>
///   <item><description>
///     <c>GET menus/user/{userId}</c> — cuelga de <c>CanManageUsers</c>, ajena a este módulo. Ver la
///     nota de <see cref="RolesAutorizacionCalculos"/> sobre por qué esa policy no se toca acá.
///   </description></item>
///   <item><description>
///     Las tres escrituras del árbol de menús — ya reservadas al administrador de la aplicación
///     (<c>AdminAplicacion</c>) desde el 15-ago-2026, un gate <b>más</b> estricto que este filtro.
///   </description></item>
/// </list>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RolesPermisoNoRequeridoAttribute : Attribute;

/// <summary>
/// Marca un endpoint como lectura del <b>catálogo global de menús</b>: en vez de la regla de roles,
/// <see cref="RolesGestionFilterAttribute"/> le aplica
/// <see cref="RolesAutorizacionCalculos.PuedeLeerCatalogoMenus"/>.
///
/// <para>
/// Va en los dos endpoints que devuelven el árbol completo —<c>GET /api/Roles/menus/tree</c> y
/// <c>GET /api/Menu/tree</c>—, que hasta hoy colgaban de <c>CanManageMenus</c>, o sea de nada.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class CatalogoMenusLecturaAttribute : Attribute;

/// <summary>
/// Exige el permiso que corresponde para administrar <b>roles, permisos y el catálogo de menús</b>.
///
/// <para>
/// <b>Qué cierra.</b> Hasta el 5-sep-2026 los diez endpoints de roles y permisos colgaban de la
/// policy <c>CanManageRoles</c>, que <c>Program.cs</c> definía como <c>RequireAuthenticatedUser()</c>
/// con un <c>TODO(seguridad)</c> al lado. Probado en vivo con el JWT de un usuario sin ningún permiso
/// de administración: <c>POST /api/Roles/{id}/permissions/assign</c> devolvía <b>404, no 403</b> — la
/// autorización pasaba y sólo lo frenaba que el rol no existiera. Como las keys se hornean en el
/// token al login, quien escribe <c>role_permissions</c> se asigna cualquier permiso, vuelve a
/// entrar y se salta todos los demás gates del sistema.
/// </para>
///
/// <para>
/// <b>Por qué un filtro de clase y no un <c>if</c> por acción</b>: mismo criterio que
/// <see cref="GestionUsuariosEscrituraFilterAttribute"/> y
/// <see cref="CargaMasivaPermisoFilterAttribute"/> — con el filtro en la clase, un endpoint nuevo
/// <b>nace cubierto</b> y hay que sacarlo explícitamente (con
/// <see cref="RolesPermisoNoRequeridoAttribute"/>) para abrirlo. Repetir la guarda catorce veces
/// convierte «se olvidaron de una» en cuestión de tiempo, y el endpoint olvidado no falla:
/// <b>deja pasar</b>. Es exactamente lo que pasó acá.
/// </para>
///
/// <para>
/// <b>Por qué no alcanza con endurecer la policy <c>CanManageRoles</c></b>, que sería lo obvio: una
/// policy no distingue lectura de escritura, y acá la lectura necesita una OR con
/// <c>usuarios.gestionar</c> que la escritura no debe tener (ver
/// <see cref="RolesAutorizacionCalculos"/>: sin esa OR, 4 usuarios medidos se quedan sin el
/// desplegable de roles del modal de usuarios). Las policies se conservan porque los atributos las
/// nombran, pero el gate real vive acá.
/// </para>
///
/// <para>
/// <b>Regla por defecto de la clase:</b> <c>GET</c> ⇒
/// <see cref="RolesAutorizacionCalculos.PuedeLeerRoles"/>; cualquier otro método ⇒
/// <see cref="RolesAutorizacionCalculos.PuedeGestionarRoles"/>. Los marcadores de arriba son las dos
/// únicas excepciones.
/// </para>
///
/// <para>Responde <b>403</b> (no 401): la sesión es válida, lo que falta es la autorización.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RolesGestionFilterAttribute : ActionFilterAttribute
{
    /// <inheritdoc />
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var metadata = context.ActionDescriptor.EndpointMetadata;

        if (metadata.OfType<RolesPermisoNoRequeridoAttribute>().Any())
        {
            base.OnActionExecuting(context);
            return;
        }

        var usuario = context.HttpContext.User;

        // Los dos ejes que no salen del claim `permission`: el DATO `users.is_super_admin` (que viaja
        // como claim) y el nombre de rol. Se leen igual que en la policy `AdminAplicacion` de
        // Program.cs, para que las dos superficies decidan con la misma información.
        var esSuperAdmin = AdministracionEmpresasAutorizacionCalculos.LeerMarcaSuperAdmin(
            usuario.FindFirst("is_super_admin")?.Value);
        var roles = usuario.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value);
        var permisos = usuario.FindAll("permission").Select(c => c.Value).ToArray();

        var (permitido, mensaje) = metadata.OfType<CatalogoMenusLecturaAttribute>().Any()
            ? (RolesAutorizacionCalculos.PuedeLeerCatalogoMenus(esSuperAdmin, roles, permisos),
               RolesAutorizacionCalculos.MensajeSinPermisoMenus)
            : RolesAutorizacionCalculos.EsLectura(context.HttpContext.Request.Method)
                ? (RolesAutorizacionCalculos.PuedeLeerRoles(esSuperAdmin, roles, permisos),
                   RolesAutorizacionCalculos.MensajeSinPermisoLecturaRoles)
                : (RolesAutorizacionCalculos.PuedeGestionarRoles(esSuperAdmin, roles, permisos),
                   RolesAutorizacionCalculos.MensajeSinPermisoRoles);

        if (!permitido)
        {
            context.Result = new ObjectResult(new { message = mensaje, error = mensaje })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        base.OnActionExecuting(context);
    }
}
