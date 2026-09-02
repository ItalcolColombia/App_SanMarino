namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica PURA del gating del dashboard: la regla que decide si un usuario tiene acceso a un módulo
/// a partir de las routes de su menú. Sin EF ni estado.
///
/// <para><b>Por qué acá y no un catálogo espejo del front.</b> El front decide qué paneles dibuja
/// leyendo `session.menu` (que ya trae la sesión) y el backend corta por su cuenta en cada endpoint
/// — el patrón de defensa en profundidad que el repo ya usa con <c>HasPermissionDirective</c> +
/// <c>permissionGuard</c> + el corte del service. Lo que se comparte entre los dos lados es esta
/// <b>regla de match</b>, no la lista de paneles: duplicar el catálogo en dos lenguajes crea dos
/// listas que se desincronizan, y la que manda termina siendo la que nadie miró.</para>
///
/// <para><b>Se compara por ROUTE, jamás por id de menú.</b> Los ids difieren local↔prod, así que un
/// mapeo por id funciona en la máquina de quien lo escribió y falla en producción sin avisar.</para>
/// </summary>
public static class DashboardCalculos
{
    /// <summary>
    /// Normaliza una route para comparar: minúsculas, con barra inicial, sin barra final.
    /// Devuelve <c>null</c> si no hay nada que normalizar.
    /// </summary>
    public static string? NormalizarRoute(string? route)
    {
        var limpia = route?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(limpia)) return null;

        var conBarra = limpia[0] == '/' ? limpia : "/" + limpia;
        return conBarra.Length > 1 ? conBarra.TrimEnd('/') : conBarra;
    }

    /// <summary>
    /// ¿La route de un menú cubre la del módulo?
    ///
    /// <para>Cubre si es la misma o si es un descendiente: <c>/vacunacion/cronograma</c> cubre
    /// <c>/vacunacion</c>. La barra del prefijo es deliberada — sin ella
    /// <c>/vacunacion-historica</c> cubriría <c>/vacunacion</c>, que son módulos distintos.</para>
    /// </summary>
    public static bool Cubre(string? routeMenu, string? routeModulo)
    {
        var menu = NormalizarRoute(routeMenu);
        var modulo = NormalizarRoute(routeModulo);
        if (menu is null || modulo is null) return false;

        return menu == modulo || menu.StartsWith(modulo + "/", StringComparison.Ordinal);
    }

    /// <summary>
    /// ¿El usuario tiene en su menú alguno de los módulos pedidos?
    ///
    /// <para><b>Fail-closed en los dos bordes:</b> sin routes en el menú ⇒ <c>false</c>; sin módulos
    /// pedidos ⇒ <c>false</c> también. Lo segundo importa: un llamador que se olvide de declarar el
    /// módulo del endpoint debe quedarse sin datos, no ver toda la empresa. Es el mismo criterio con
    /// el que los <c>p_scope_*</c> de vacunación se declararon obligatorios.</para>
    /// </summary>
    public static bool TieneAlgunModulo(IEnumerable<string?>? routesMenu, IEnumerable<string?>? routesModulo)
    {
        if (routesMenu is null || routesModulo is null) return false;

        var propias = routesMenu
            .Select(NormalizarRoute)
            .Where(r => r is not null)
            .ToHashSet(StringComparer.Ordinal);

        if (propias.Count == 0) return false;

        var pedidas = routesModulo.Select(NormalizarRoute).Where(r => r is not null).ToList();
        if (pedidas.Count == 0) return false;

        return pedidas.Any(modulo => propias.Any(propia => Cubre(propia, modulo)));
    }

    /// <summary>
    /// ¿El usuario tiene alguno de los permisos pedidos? Sin permisos pedidos ⇒ <c>true</c>
    /// (el bloque no exige acción). Comparación sin distinguir mayúsculas, como
    /// <c>fn_menu_usuario</c>, que baja las keys a minúsculas antes de cruzar.
    /// </summary>
    public static bool TieneAlgunPermiso(IEnumerable<string?>? permisos, IEnumerable<string?>? pedidos)
    {
        var requeridos = pedidos?
            .Select(p => p?.Trim().ToLowerInvariant())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        if (requeridos is null || requeridos.Count == 0) return true;
        if (permisos is null) return false;

        var propios = permisos
            .Select(p => p?.Trim().ToLowerInvariant())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToHashSet(StringComparer.Ordinal);

        return requeridos.Any(propios.Contains);
    }

    /// <summary>
    /// Routes de módulo de cada panel. Es la MISMA tabla que
    /// <c>features/dashboard/models/dashboard-panel.model.ts</c>, y existe acá sólo para que el
    /// endpoint sepa qué módulo exigir. Si se agrega un panel, se agrega en los dos lados — y los
    /// tests de cada lado lo cubren.
    /// </summary>
    public static class ModulosPanel
    {
        public static readonly string[] Postura = { "/daily-log/seguimiento", "/daily-log/produccion" };
        public static readonly string[] Engorde = { "/daily-log/aves-engorde" };
        public static readonly string[] AlimentoInventario = { "/gestion-inventario", "/inventario-gastos" };
        public static readonly string[] Cumplimiento = { "/vacunacion", "/cuadres-offline", "/implementacion" };
    }
}
