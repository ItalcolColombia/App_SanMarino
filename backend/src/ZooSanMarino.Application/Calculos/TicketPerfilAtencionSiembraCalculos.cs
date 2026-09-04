using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Con qué <b>perfil de atención</b> de tickets nace una empresa: qué rol queda como resolutor y de
/// qué tipos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe.</b> Un tipo de ticket <b>sin resolutor en la empresa no se ofrece</b>:
/// <c>TicketPerfilService.GetTiposPermitidosAsync</c> descarta todo tipo cuyo listado de asignables
/// venga vacío. Como <c>CompanyService.CreateAsync</c> sembraba <c>company_permissions</c> y nada
/// más, la empresa nueva nacía sin una sola fila en <c>ticket_resolutor_rol</c> ⇒ el formulario de
/// «Nuevo caso» mostraba el desplegable de Tipo vacío y, siendo <c>required</c>, no se podía enviar.
/// Sin error, sin log: la request devuelve <c>200 []</c>. Le pasó a <b>Santa Reyes</b>, que estuvo
/// sin poder abrir un caso a desarrollo desde su alta (jul-2026).
/// </para>
/// <para>
/// <b>Cálculo PURO</b> (sin EF, sin estado): recibe los roles que existen y las filas que la empresa
/// ya tiene, y devuelve las que faltan. El service sólo persiste el resultado.
/// </para>
/// </remarks>
public static class TicketPerfilAtencionSiembraCalculos
{
    /// <summary>
    /// Nombres de rol que cuentan como <b>equipo de desarrollo</b>, el resolutor global del módulo.
    /// </summary>
    /// <remarks>
    /// La comparación es <b>exacta</b> (ignorando mayúsculas y espacios al borde), nunca por
    /// «contiene». En la base conviven <c>Admin Panama</c>, <c>Admin Demo</c>,
    /// <c>Ecuador Administrador</c>, <c>Santa Reyes Administrador</c> y <c>ADMINISTRADOR DE GRANJA</c>:
    /// son administradores <b>de su empresa</b>, no el equipo que atiende los casos. Con un substring
    /// la empresa nueva nacería asignándose sus propios tickets de desarrollo.
    /// <para>
    /// Misma frontera —y por el mismo motivo— que
    /// <see cref="CatalogoGlobalAutorizacionCalculos.RolesAdminAplicacion"/>. Se declara aparte a
    /// propósito: aquélla decide una <b>autorización</b> y ésta una <b>siembra de datos</b>; que hoy
    /// coincidan no debe hacer que tocar una mueva la otra en silencio.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlySet<string> RolesResolutorGlobal =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "admin", "administrador" };

    /// <summary>
    /// Tipos que atiende el resolutor global en una empresa recién creada: <b>los cuatro</b>.
    /// </summary>
    /// <remarks>
    /// Una empresa nueva todavía no tiene personal propio configurado, así que el único destino
    /// posible es el equipo de desarrollo. Sembrar sólo <c>DESARROLLO</c> dejaría a la empresa sin
    /// poder abrir siquiera un caso de <c>SOPORTE</c>. Es el mismo criterio con que
    /// <c>company_permissions</c> nace con el catálogo completo: se abre lo necesario para que el
    /// módulo funcione, y la empresa lo recorta después desde Configuración → Perfil de atención.
    /// El orden es el de declaración y es estable: lo fija un test.
    /// </remarks>
    public static readonly IReadOnlyList<string> TiposEmpresaNueva = new[]
    {
        TicketTipos.Soporte,
        TicketTipos.Dudas,
        TicketTipos.Desarrollo,
        TicketTipos.Requerimiento,
    };

    /// <summary>Una fila de <c>ticket_resolutor_rol</c> a insertar (siempre con <c>pais_id</c> NULL).</summary>
    public readonly record struct FilaResolutorRol(int RoleId, string Tipo);

    /// <summary>¿Este nombre de rol es el del equipo de desarrollo?</summary>
    /// <returns>
    /// <c>true</c> sólo con coincidencia <b>exacta</b> contra <see cref="RolesResolutorGlobal"/>.
    /// <b>Fail-closed:</b> <c>null</c> o en blanco ⇒ <c>false</c>.
    /// </returns>
    public static bool EsResolutorGlobal(string? nombreRol) =>
        !string.IsNullOrWhiteSpace(nombreRol) && RolesResolutorGlobal.Contains(nombreRol.Trim());

    /// <summary>
    /// Filas que le faltan a la empresa para que su perfil de atención quede completo.
    /// </summary>
    /// <param name="roles">Roles existentes en el sistema, como <c>(id, nombre)</c>.</param>
    /// <param name="existentes">
    /// Pares <c>(role_id, tipo)</c> que la empresa YA tiene en <c>ticket_resolutor_rol</c>. Se
    /// respetan tal cual: esta función nunca propone pisar una configuración hecha a mano.
    /// </param>
    /// <returns>
    /// Las filas a insertar, ordenadas por rol y luego por el orden de
    /// <see cref="TiposEmpresaNueva"/>. Sin duplicados. <b>Fail-closed:</b> si ningún rol es el
    /// global, devuelve vacío — no se inventa un resolutor.
    /// </returns>
    public static IReadOnlyList<FilaResolutorRol> FilasFaltantes(
        IEnumerable<(int Id, string? Nombre)>? roles,
        IEnumerable<(int RoleId, string Tipo)>? existentes)
    {
        if (roles is null) return Array.Empty<FilaResolutorRol>();

        var yaEstan = new HashSet<(int, string)>();
        foreach (var (roleId, tipo) in existentes ?? Enumerable.Empty<(int, string)>())
        {
            if (string.IsNullOrWhiteSpace(tipo)) continue;
            yaEstan.Add((roleId, tipo.Trim().ToUpperInvariant()));
        }

        var resultado = new List<FilaResolutorRol>();
        var emitidas = new HashSet<(int, string)>();

        foreach (var id in roles.Where(r => EsResolutorGlobal(r.Nombre))
                                .Select(r => r.Id)
                                .Distinct()
                                .OrderBy(id => id))
        {
            foreach (var tipo in TiposEmpresaNueva)
            {
                if (yaEstan.Contains((id, tipo))) continue;
                if (!emitidas.Add((id, tipo))) continue;
                resultado.Add(new FilaResolutorRol(id, tipo));
            }
        }

        return resultado;
    }
}
