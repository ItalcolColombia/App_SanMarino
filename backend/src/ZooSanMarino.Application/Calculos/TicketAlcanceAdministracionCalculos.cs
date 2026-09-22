// src/ZooSanMarino.Application/Calculos/TicketAlcanceAdministracionCalculos.cs
// Regla PURA: sobre qué EMPRESAS administra tickets una sesión.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Hasta dónde llega la administración de tickets: todas las empresas, la empresa activa o nada.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe (F4, 19-sep-2026).</b> <c>tickets.admin</c> se usaba como «administración global»:
/// tablero, roadmap, panel, búsqueda global, detalle de cualquier caso y registrar casos «a nombre de»
/// cualquier usuario, de TODAS las empresas. Pero ese permiso lo tienen roles de UNA empresa
/// (<c>Santa Reyes Administrador</c>, <c>Admin Demo</c>, <c>Lider Demanda &amp; Delivery</c>). Y
/// <c>GET api/tickets/global</c> ni siquiera lo pedía: cualquier sesión listaba los casos de todas las
/// empresas.
/// </para>
/// <para>
/// <b>La regla.</b> Todas las empresas = admin global (<c>AdminEmpresas</c>) <b>y</b> el permiso de la
/// vista. El permiso sin admin global administra solo la <b>empresa activa</b>. Sin permiso, nada (o
/// solo lo asignado, según la vista). Para el admin global el resultado es idéntico al anterior.
/// </para>
/// </remarks>
public static class TicketAlcanceAdministracionCalculos
{
    public const string PermisoAdmin = "tickets.admin";
    public const string PermisoGestionar = "tickets.gestionar";

    /// <summary>Alcance de una vista de administración.</summary>
    public enum Alcance
    {
        /// <summary>Sin alcance de administración (en el tablero: solo lo asignado a mí).</summary>
        Ninguno = 0,
        /// <summary>Solo los tickets de la empresa activa.</summary>
        EmpresaActiva = 1,
        /// <summary>Los tickets de todas las empresas.</summary>
        Todas = 2,
    }

    private static bool Tiene(IEnumerable<string>? permisos, string key) =>
        permisos?.Contains(key, StringComparer.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Alcance de tablero / roadmap / panel / reporte. Conserva la regla del permiso por vista de
    /// <see cref="TicketAlcancePanelCalculos.TieneAlcanceGlobal"/> (indicadores solo en las vistas de
    /// lectura) y le suma el eje de empresa.
    /// </summary>
    public static Alcance AlcanceTablero(bool esAdminEmpresas, IEnumerable<string>? permisos, bool vistaSoloLectura)
    {
        if (!TicketAlcancePanelCalculos.TieneAlcanceGlobal(permisos, vistaSoloLectura)) return Alcance.Ninguno;
        return esAdminEmpresas ? Alcance.Todas : Alcance.EmpresaActiva;
    }

    /// <summary>
    /// Alcance de la bandeja de administración (<c>GET api/tickets/global</c>, su lista de resolutores,
    /// el buscador de solicitantes «a nombre de» y el detalle de casos ajenos): exige
    /// <c>tickets.admin</c>.
    /// </summary>
    public static Alcance AlcanceAdministracion(bool esAdminEmpresas, IEnumerable<string>? permisos)
    {
        if (!Tiene(permisos, PermisoAdmin)) return Alcance.Ninguno;
        return esAdminEmpresas ? Alcance.Todas : Alcance.EmpresaActiva;
    }

    /// <summary>¿Un alcance cubre un caso de <paramref name="empresaCaso"/>?</summary>
    public static bool Cubre(Alcance alcance, int empresaActiva, int empresaCaso) => alcance switch
    {
        Alcance.Todas => true,
        Alcance.EmpresaActiva => empresaCaso == empresaActiva,
        _ => false,
    };

    /// <summary>
    /// ¿Puede GESTIONAR (planificar, mover, anotar como equipo) este caso? Hace falta el permiso
    /// (<c>tickets.admin</c> o <c>tickets.gestionar</c>) como siempre; lo nuevo es la EMPRESA: el
    /// permiso alcanza para los casos de la empresa activa, para los que la persona tiene asignados (así
    /// un resolutor global trabaja los casos de cualquier empresa que le llegan) y, para el admin
    /// global, para todos. Antes bastaba el permiso para gestionar un caso de cualquier empresa por id.
    /// </summary>
    public static bool PuedeGestionarCaso(
        bool esAdminEmpresas, IEnumerable<string>? permisos, int empresaActiva, int empresaCaso, bool asignadoAMi)
    {
        if (!Tiene(permisos, PermisoAdmin) && !Tiene(permisos, PermisoGestionar)) return false;
        return esAdminEmpresas || asignadoAMi || empresaCaso == empresaActiva;
    }
}
