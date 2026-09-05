namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Quién puede ESCRIBIR en los catálogos globales del sistema: el catálogo de permisos
/// (<c>permissions</c>) y el árbol de menús (<c>menus</c>).
/// </summary>
/// <remarks>
/// <para>
/// No son configuración de una empresa ni de un rol: son estructuras que comparten TODAS las
/// empresas. Borrar una key de permiso o un ítem de menú acá se lo lleva puesto a todo el mundo, en
/// todos los países. Por eso la escritura queda reservada al <b>administrador de la aplicación</b>.
/// </para>
/// <para>
/// <b>Las lecturas NO pasan por acá.</b> Un usuario no admin necesita
/// <c>GET /api/Permission</c> para asignarle permisos a un rol y <c>GET /api/Roles/menus/tree</c>
/// para que la tabla de roles muestre etiquetas de menú en vez de ids. Cerrar las lecturas rompería
/// el módulo de Roles para todos.
/// </para>
/// <para>
/// Cálculo PURO: sin EF, sin <c>HttpContext</c>, sin estado. Recibe los nombres de rol de la sesión
/// y responde. Espejo del front:
/// <c>frontend/src/app/features/config/role-management/funciones/catalogos-globales.funcion.ts</c>
/// — el front decide qué se MUESTRA, esto decide qué se PUEDE.
/// </para>
/// </remarks>
public static class CatalogoGlobalAutorizacionCalculos
{
    /// <summary>
    /// Nombres de rol que cuentan como administrador de la aplicación.
    /// </summary>
    /// <remarks>
    /// La comparación es <b>exacta</b> (ignorando mayúsculas y espacios al borde), nunca por
    /// «contiene». En la base conviven <c>Admin Panama</c>, <c>Admin Demo</c>,
    /// <c>Ecuador Administrador</c>, <c>Santa Reyes Administrador</c> y
    /// <c>ADMINISTRADOR DE GRANJA</c>: son administradores <b>de su empresa</b>. Con una
    /// comparación por substring todos ellos entrarían al catálogo global, que es justo lo que hay
    /// que evitar.
    /// </remarks>
    public static readonly IReadOnlySet<string> RolesAdminAplicacion =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "admin", "administrador" };

    /// <summary>
    /// ¿Alguno de estos roles es el del administrador de la aplicación?
    /// </summary>
    /// <param name="rolesDeLaSesion">
    /// Nombres de rol del usuario autenticado (claims <c>ClaimTypes.Role</c>).
    /// </param>
    /// <returns>
    /// <c>true</c> solo si hay coincidencia exacta con un rol de <see cref="RolesAdminAplicacion"/>.
    /// <b>Fail-closed:</b> <c>null</c>, lista vacía o entradas en blanco ⇒ <c>false</c>.
    /// </returns>
    public static bool PuedeEscribirCatalogoGlobal(IEnumerable<string?>? rolesDeLaSesion)
    {
        if (rolesDeLaSesion is null) return false;

        foreach (var rol in rolesDeLaSesion)
        {
            if (string.IsNullOrWhiteSpace(rol)) continue;
            if (RolesAdminAplicacion.Contains(rol.Trim())) return true;
        }

        return false;
    }
}
