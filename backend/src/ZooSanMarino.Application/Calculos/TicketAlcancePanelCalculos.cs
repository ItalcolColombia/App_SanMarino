namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Decide si el usuario ve <b>todos</b> los casos o solo los suyos en las vistas agregadas de
/// ItalJira (tablero, roadmap, panel de control y su reporte).
/// </summary>
/// <remarks>
/// <para>
/// Existe para poder abrir el <b>Panel de control</b> a gerencia sin regalarle
/// <c>tickets.admin</c>. Ese permiso no es solo «ver todo»: también habilita crear casos a nombre de
/// otro, gestionar/mover cualquier caso, el buscador de solicitantes y la Configuración de ItalJira.
/// Un gerente necesita el número, no el volante.
/// </para>
/// <para>
/// Por eso el alcance se decide <b>por vista</b> y no por endpoint: <c>tickets.indicadores</c> abre
/// únicamente las vistas de SOLO LECTURA (indicadores y reporte). El tablero y el roadmap siguen
/// exigiendo <c>tickets.admin</c>, así que el permiso nuevo no los abre ni llamando la API a mano.
/// </para>
/// <para>
/// Lógica pura (sin EF ni estado) a propósito: es la regla que decide qué datos ve quién, y como tal
/// está cubierta por tests. Con <c>tickets.indicadores</c> ausente el resultado es idéntico al
/// comportamiento previo en las cuatro vistas.
/// </para>
/// </remarks>
public static class TicketAlcancePanelCalculos
{
    /// <summary>Administración global del módulo. Ve todo, en cualquier vista.</summary>
    public const string PermisoAdmin = "tickets.admin";

    /// <summary>Lectura global del panel de control y su reporte. NO habilita gestión.</summary>
    public const string PermisoIndicadores = "tickets.indicadores";

    /// <summary>Las keys se comparan igual que en el resto del módulo: sin distinguir mayúsculas.</summary>
    private static readonly StringComparer ComparadorKeys = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// True si el usuario ve el conjunto COMPLETO de casos; false si hay que recortarlo a los que
    /// tiene asignados.
    /// </summary>
    /// <param name="permisos">Permisos efectivos del usuario actual. Null o vacío ⇒ false.</param>
    /// <param name="vistaSoloLectura">
    /// True solo en indicadores y reporte. En tablero y roadmap va false, porque ahí «ver todo»
    /// viene acompañado de las acciones de gestión sobre lo que se ve.
    /// </param>
    public static bool TieneAlcanceGlobal(IEnumerable<string>? permisos, bool vistaSoloLectura)
    {
        if (permisos is null) return false;

        var keys = permisos as IReadOnlyCollection<string> ?? permisos.ToList();
        if (keys.Count == 0) return false;

        if (keys.Contains(PermisoAdmin, ComparadorKeys)) return true;

        return vistaSoloLectura && keys.Contains(PermisoIndicadores, ComparadorKeys);
    }
}
