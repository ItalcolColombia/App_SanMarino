using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Calculos;

/// <summary>Un menú del catálogo, plano, con lo que hace falta para decidir si se ve.</summary>
/// <param name="KeysRequeridas">
/// Keys de <c>menu_permissions</c>. Vacío = el ítem no exige ningún permiso.
/// </param>
public sealed record MenuPlano(
    int Id,
    string? Label,
    string? Icon,
    string? Route,
    int Orden,
    int? ParentId,
    IReadOnlyCollection<string> KeysRequeridas
);

/// <summary>
/// Qué ítems del menú ve un usuario dentro de una empresa. Lógica PURA: sin EF, sin estado.
///
/// <para>
/// <b>Esta clase es la especificación ejecutable de <c>fn_menu_usuario</c>.</b> En runtime el menú lo
/// arma la función SQL de una sola llamada (plan
/// <c>fase_de_desarrollo/menu_efectivo_por_empresa_plan.md</c>); acá vive la misma regla en C# para
/// que los tests sean el contrato que esa función tiene que cumplir. Es el patrón de
/// <c>SeguimientoAvesEngordeCalculos</c>: si un resultado se calcula en SQL y en C#, uno de los dos
/// es el dueño y el otro es el test.
/// </para>
///
/// <para>Las reglas:</para>
/// <list type="bullet">
///   <item><b>D1</b> — «habilitado para la empresa» = fila en <c>company_menus</c> con
///         <c>is_enabled = true</c>. Fila ausente y <c>is_enabled = false</c> ocultan igual.</item>
///   <item><b>D2</b> — empresa SIN ninguna fila en <c>company_menus</c> ⇒ no se filtra (fail-open por
///         empresa). Ver <see cref="ResolverVisibles"/> para por qué no puede ser fail-closed.</item>
///   <item><b>D3</b> — el gate de empresa se aplica a la SEMILLA, antes de subir por los ancestros:
///         un grupo padre no habilitado pero con hijos habilitados se muestra igual, porque si no el
///         submenú entero desaparece.</item>
///   <item><b>D4</b> — el orden sale de <c>menus."order"</c>, nunca de
///         <c>company_menus.sort_order</c>. Empate de <c>order</c> ⇒ desempata el <c>id</c>.</item>
///   <item><b>D5</b> — el gate de empresa <b>no aplica al super admin</b>. Es el único que se para
///         en cualquier empresa, y los ítems que administran el sistema entero viven habilitados en
///         una sola: sin esta excepción, limpiarle los menús a esa empresa lo encierra fuera de la
///         pantalla que sirve para revertirlo. Ver <see cref="EmpresaFiltra"/>.</item>
/// </list>
/// </summary>
public static class MenuVisibilidadCalculos
{
    /// <summary>
    /// Con las que el backend deserializa lo que devuelve <c>fn_menu_usuario</c>. Viven acá —y no en
    /// el service— para que el test que fija el contrato use exactamente las mismas.
    /// La función emite camelCase (<c>id</c>, <c>label</c>, <c>icon</c>, <c>route</c>, <c>order</c>,
    /// <c>children</c>), que es el mismo JSON que ya viajaba al front.
    /// </summary>
    public static readonly System.Text.Json.JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Las keys de permiso se comparan sin distinguir mayúsculas.</summary>
    public static readonly StringComparer ComparadorKeys = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// D2 — ¿el gate por empresa está activo? Sólo si hay una empresa Y alguien la configuró, y el
    /// usuario no es super admin (D5).
    /// </summary>
    /// <param name="companyId">Empresa efectiva; <c>null</c> = consulta sin empresa (administración).</param>
    /// <param name="filasEnCompanyMenus">Cuántas filas tiene la empresa en <c>company_menus</c>.</param>
    /// <param name="esSuperAdmin">
    /// Marca <c>users.is_super_admin</c>. <b>D5</b>: al super admin no se le aplica el gate de
    /// empresa. El resto del cálculo no cambia — sigue viendo sólo lo que le dan sus
    /// <c>role_menus</c>, dentro de <c>menus.is_active</c> y pasando <c>menu_permissions</c>.
    ///
    /// <para>
    /// <b>Por qué</b>: el super admin es el único que puede pararse en cualquier empresa, y su menú
    /// se armaba con el <c>company_menus</c> de la empresa activa como el de cualquiera. Los ítems
    /// que administran el sistema entero —Empresas y db_studio— están habilitados en <b>una sola
    /// empresa</b>, así que quitárselos a esa empresa lo dejaba sin el módulo Empresas en TODA la
    /// app y sin ruta de vuelta por la UI: para rehabilitarlo hay que entrar a
    /// Configuración → Empresas → Menús, que es justo el menú que desapareció. El fail-open de D2 no
    /// cubre el caso, porque aplica a la empresa sin ninguna fila y todas tienen.
    /// </para>
    ///
    /// <para>
    /// Por defecto <c>false</c>: sin la marca, el resultado es <b>idéntico</b> al previo.
    /// </para>
    /// </param>
    public static bool EmpresaFiltra(int? companyId, int filasEnCompanyMenus, bool esSuperAdmin = false) =>
        !esSuperAdmin && companyId is not null && filasEnCompanyMenus > 0;

    /// <summary>
    /// Los ítems que el usuario ve, planos y ordenados, listos para <see cref="ConstruirArbol"/>.
    /// </summary>
    /// <param name="activos">Catálogo de menús con <c>is_active = true</c>.</param>
    /// <param name="asignados">
    /// Ids de <c>role_menus</c> de los roles del usuario en la empresa. Vacío ⇒ rama fallback: se
    /// parte del catálogo entero filtrado por permisos.
    /// </param>
    /// <param name="keysUsuario">Keys de permiso efectivas del usuario.</param>
    /// <param name="habilitadosEmpresa">
    /// Ids habilitados por la empresa (D1). <b><c>null</c> = el gate no aplica</b> (D2): la empresa no
    /// tiene ninguna fila, o la consulta no trae empresa.
    ///
    /// <para>
    /// No puede ser fail-closed: <c>CompanyService.CreateAsync</c> siembra <c>company_permissions</c>
    /// pero NO <c>company_menus</c>, así que una empresa nueva nacería con el menú vacío y sin forma
    /// de arreglarlo desde la app — para asignar menús hay que entrar a Configuración, que es un ítem
    /// del menú que no se vería. Fail-open sobre la tabla vacía no puede empeorar el comportamiento
    /// previo, donde el gate no existía.
    /// </para>
    /// </param>
    public static IReadOnlyList<MenuPlano> ResolverVisibles(
        IEnumerable<MenuPlano> activos,
        IEnumerable<int> asignados,
        IEnumerable<string> keysUsuario,
        IReadOnlyCollection<int>? habilitadosEmpresa)
    {
        var catalogo = (activos ?? Array.Empty<MenuPlano>()).ToList();
        if (catalogo.Count == 0) return Array.Empty<MenuPlano>();

        var porId = catalogo
            .GroupBy(m => m.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var keys = new HashSet<string>(keysUsuario ?? Array.Empty<string>(), ComparadorKeys);

        // Ojo con la diferencia entre estas dos: la RAMA se decide con los ids CRUDOS de
        // `role_menus`, pero la semilla sólo puede traer menús activos. Un usuario cuyos role_menus
        // apunten todos a menús inactivos se queda sin menú — NO cae al fallback, que sería más
        // permisivo justo donde no corresponde. Es lo que hacía el C# anterior.
        var asignadosCrudos = new HashSet<int>(asignados ?? Array.Empty<int>());
        var hayAsignados = asignadosCrudos.Count > 0;
        var asignadosSet = new HashSet<int>(asignadosCrudos.Where(porId.ContainsKey));

        bool PasaPermisos(MenuPlano m) =>
            m.KeysRequeridas is null ||
            m.KeysRequeridas.Count == 0 ||
            m.KeysRequeridas.Any(k => keys.Contains(k));

        bool HabilitaEmpresa(int id) =>
            habilitadosEmpresa is null || habilitadosEmpresa.Contains(id);

        // D3: el gate de empresa se aplica a la semilla, ANTES de subir por los ancestros.
        var semilla = hayAsignados
            ? catalogo.Where(m => asignadosSet.Contains(m.Id) && HabilitaEmpresa(m.Id))
            : catalogo.Where(m => PasaPermisos(m) && HabilitaEmpresa(m.Id));

        var incluidos = new HashSet<int>();
        foreach (var m in semilla)
        {
            incluidos.Add(m.Id);
            var pid = m.ParentId;
            // Se sube por la cadena de padres dentro del catálogo activo. Una cadena cortada por un
            // ancestro inactivo deja al nodo huérfano y el armado del árbol lo descarta.
            while (pid.HasValue && porId.TryGetValue(pid.Value, out var padre) && incluidos.Add(padre.Id))
                pid = padre.ParentId;
        }

        // En la rama asignada el filtro de permisos alcanza también a los ancestros; en el fallback
        // no, porque ahí los ancestros entraron por fuera del filtro. Es el comportamiento previo.
        var finales = catalogo
            .Where(m => incluidos.Contains(m.Id) && (!hayAsignados || PasaPermisos(m)))
            .ToList();

        var finalesPorId = finales.ToDictionary(m => m.Id);

        // Sólo lo alcanzable desde una raíz visible se pinta: un nodo cuyo padre quedó afuera no es
        // hijo de nadie ni raíz, así que desaparece.
        bool AlcanzableDesdeRaiz(MenuPlano m)
        {
            var actual = m;
            var guarda = 0;
            while (actual.ParentId is int pid)
            {
                if (!finalesPorId.TryGetValue(pid, out var padre)) return false;
                actual = padre;
                if (++guarda > finales.Count) return false;   // ciclo defensivo
            }
            return true;
        }

        return finales
            .Where(AlcanzableDesdeRaiz)
            .OrderBy(m => m.ParentId.HasValue ? 1 : 0)
            .ThenBy(m => m.ParentId ?? 0)
            .ThenBy(m => m.Orden)
            .ThenBy(m => m.Id)
            .ToList();
    }

    /// <summary>
    /// Arma el árbol que consume el sidebar. Hijos ordenados por <c>order</c> y, ante empate, por
    /// <c>id</c> (D4).
    /// </summary>
    public static MenuItemDto[] ConstruirArbol(IEnumerable<MenuPlano> planos)
    {
        var lista = (planos ?? Array.Empty<MenuPlano>()).ToList();
        if (lista.Count == 0) return Array.Empty<MenuItemDto>();

        var hijos = lista
            .Where(m => m.ParentId.HasValue)
            .GroupBy(m => m.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Orden).ThenBy(x => x.Id).ToList());

        MenuItemDto Nodo(MenuPlano m) => new(
            m.Id,
            m.Label ?? string.Empty,
            m.Icon,
            m.Route,
            m.Orden,
            hijos.TryGetValue(m.Id, out var kids)
                ? kids.Select(Nodo).ToArray()
                : Array.Empty<MenuItemDto>());

        return lista
            .Where(m => m.ParentId is null)
            .OrderBy(m => m.Orden)
            .ThenBy(m => m.Id)
            .Select(Nodo)
            .ToArray();
    }
}
