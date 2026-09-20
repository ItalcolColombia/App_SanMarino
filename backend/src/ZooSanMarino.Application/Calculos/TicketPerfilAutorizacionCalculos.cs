// src/ZooSanMarino.Application/Calculos/TicketPerfilAutorizacionCalculos.cs
// Regla PURA: quién puede configurar el perfil de tickets de un usuario o de un rol.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Quién puede ESCRIBIR la configuración de tickets: el nivel de apertura de un usuario o de un rol y
/// la plantilla de atención (resolutores).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe.</b> Hasta el 19-sep-2026 <c>api/ticket-perfiles</c> no tenía ningún gate:
/// cualquier sesión podía hacerse Implementador o resolutor de cualquier tipo con un <c>PUT</c> sobre su
/// propio id, o reescribir la plantilla de cualquier rol.
/// </para>
/// <para>
/// <b>La regla</b> (control interno por empresa + control del admin global):
/// <list type="number">
/// <item>El <b>admin global</b> (policy <c>AdminEmpresas</c>: super admin o rol <c>Admin</c>/<c>Administrador</c>
///   exacto) configura cualquier cosa, en cualquier empresa, con alcance EMPRESA o GLOBAL.</item>
/// <item>Tocar una fila <b>GLOBAL</b> es exclusivo del admin global.</item>
/// <item>Nadie más se configura <b>a sí mismo</b>.</item>
/// <item>El <b>administrador de tickets de la empresa</b> (<c>tickets.admin</c>) configura usuarios y
///   roles de SU empresa activa, y nada fuera de ella.</item>
/// </list>
/// </para>
/// <para>
/// Las LECTURAS no pasan por acá: el editor se abre desde Usuarios y Roles, y esos módulos ya deciden
/// quién los ve.
/// </para>
/// </remarks>
public static class TicketPerfilAutorizacionCalculos
{
    public const string PermisoAdminTickets = "tickets.admin";

    /// <summary>Resultado: si se permite y, si no, el porqué (va tal cual en el 403).</summary>
    public readonly record struct Decision(bool Permitido, string? Motivo)
    {
        public static Decision Si => new(true, null);
        public static Decision No(string motivo) => new(false, motivo);
    }

    /// <param name="esAdminEmpresas">¿La sesión es admin global?</param>
    /// <param name="permisos">Permisos de la sesión.</param>
    /// <param name="empresaActiva">Empresa activa de la sesión.</param>
    /// <param name="empresaDestino">Empresa donde vive el perfil (ya resuelta).</param>
    /// <param name="esUnoMismo">¿El usuario configurado es el mismo que la sesión?</param>
    /// <param name="tocaGlobal">¿El cambio activa, apaga, crea o convierte alguna fila GLOBAL?</param>
    public static Decision PuedeEscribir(
        bool esAdminEmpresas,
        IEnumerable<string>? permisos,
        int empresaActiva,
        int empresaDestino,
        bool esUnoMismo,
        bool tocaGlobal)
    {
        if (esAdminEmpresas) return Decision.Si;

        if (tocaGlobal)
            return Decision.No("Solo el administrador global puede configurar resolutores globales (todas las empresas).");

        if (esUnoMismo)
            return Decision.No("No podés cambiar tu propio perfil de tickets: pedíselo al administrador.");

        var esAdminTickets = permisos?.Contains(PermisoAdminTickets, StringComparer.OrdinalIgnoreCase) == true;
        if (!esAdminTickets)
            return Decision.No("Configurar tickets requiere el permiso de administrador de tickets (tickets.admin) de la empresa.");

        if (empresaDestino != empresaActiva)
            return Decision.No("Solo podés configurar los tickets de usuarios y roles de tu empresa activa.");

        return Decision.Si;
    }

    /// <summary>
    /// ¿La pantalla puede ofrecer la opción «Todas las empresas (Global)»? Solo al admin global.
    /// Espejo del front (<c>ticket-perfil-editor</c>).
    /// </summary>
    public static bool PuedeElegirGlobal(bool esAdminEmpresas) => esAdminEmpresas;
}
