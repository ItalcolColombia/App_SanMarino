namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Regla PURA de quién puede ESCRIBIR en Gestión de Usuarios
/// (`fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md` §3).
///
/// <para>
/// <b>Lo que había antes.</b> Nada. <c>UsersController</c> llevaba un <c>[Authorize]</c> de clase sin
/// política y ninguno de sus 15 endpoints miraba un permiso; <c>UserFarmController</c> tenía el
/// <c>[Authorize]</c> <b>comentado</b> y lo salvaba únicamente la <c>FallbackPolicy</c>. Cualquier
/// sesión válida podía crear, editar, borrar usuarios, resetear contraseñas y asignar granjas —
/// incluido el toggle de «administrador de granja», que es una escalada de privilegios.
/// </para>
///
/// <para>
/// <b>Este permiso separa VER de ESCRIBIR, no abre ni cierra el módulo.</b> El acceso a la pantalla
/// lo sigue dando <c>role_menus</c>; las LECTURAS quedan abiertas a propósito, que es lo pedido: sin
/// el permiso se ve el listado y el detalle del usuario, y nada más.
/// </para>
///
/// <para>
/// ⛔ <b>Por qué no se endurece la política <c>CanManageUsers</c>.</b> Esa política la usan dos
/// endpoints ajenos a este módulo —<c>RoleController.GetMenusForUser</c> y
/// <c>MenuController.GetForUser</c>—, así que exigirle esta key rompería la pantalla de Roles. El
/// gate va en el controller, con el patrón de <c>Forbid()</c> por permiso que ya usan 11 controllers
/// del repo.
/// </para>
/// </summary>
public static class GestionUsuariosAutorizacionCalculos
{
    /// <summary>
    /// Permiso que habilita crear, editar, eliminar usuarios, restablecer contraseñas y asignar
    /// granjas.
    /// </summary>
    public const string PermisoGestionar = "usuarios.gestionar";

    /// <summary>
    /// Mensaje del rechazo. Dice qué SÍ se puede hacer, para que quien lo lea no crea que perdió el
    /// acceso al módulo entero.
    /// </summary>
    public const string MensajeSinPermiso =
        "No tiene permiso para gestionar usuarios. Puede consultar el listado y el detalle, " +
        "pero no crear, editar ni eliminar.";

    /// <summary>
    /// ¿Este usuario puede ESCRIBIR en el módulo? Fail-closed: lista nula ⇒ no.
    /// Comparación ordinal, igual que el resto de los gates por permiso del repo.
    /// </summary>
    public static bool PuedeGestionar(IEnumerable<string>? permisos) =>
        permisos is not null && permisos.Contains(PermisoGestionar, StringComparer.Ordinal);

    /// <summary>
    /// ¿Es una operación de solo LECTURA del módulo? Se expone para que el criterio de qué queda
    /// abierto viva en un solo lugar y no se disperse en cada <c>if</c> del controller.
    /// </summary>
    public static bool EsLectura(string? metodoHttp) =>
        string.Equals((metodoHttp ?? string.Empty).Trim(), "GET", StringComparison.OrdinalIgnoreCase);
}
