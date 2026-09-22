// src/ZooSanMarino.Application/Calculos/TicketNivelEfectivoCalculos.cs
// Regla PURA: qué nivel de APERTURA de tickets tiene un usuario.
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Nivel efectivo de un usuario para ABRIR tickets (NORMAL ⇒ Soporte y Dudas; IMPLEMENTADOR ⇒
/// además Desarrollo y Requerimiento). No tiene nada que ver con ATENDER (resolutor).
/// </summary>
/// <remarks>
/// <para>
/// Tres fuentes, gana la mayor:
/// <list type="number">
/// <item>Permiso <c>tickets.gestionar</c> o <c>tickets.admin</c> ⇒ IMPLEMENTADOR (regla previa, intacta).</item>
/// <item><b>Nivel de sus roles</b> (<c>roles.ticket_nivel_creacion</c>) — nuevo el 19-sep-2026: el
///   administrador lo define UNA vez por rol en vez de habilitar a cada persona.</item>
/// <item>Perfil personal en esa empresa (<c>ticket_perfil_usuario</c>): la excepción por persona.</item>
/// </list>
/// </para>
/// <para>
/// <b>Equivalencia.</b> Con ningún rol que defina nivel (la columna nace NULL en todos) el resultado
/// es el mismo que antes, byte a byte, incluido devolver el nivel del perfil tal cual está guardado.
/// </para>
/// <para>
/// El perfil personal SUBE pero no BAJA: un perfil NORMAL no le quita a nadie el IMPLEMENTADOR que le
/// da su rol. Para restringir a una persona se le cambia el rol.
/// </para>
/// </remarks>
public static class TicketNivelEfectivoCalculos
{
    private static readonly string[] PermisosQueImplementan = { "tickets.gestionar", "tickets.admin" };

    /// <param name="permisos">Permisos de la sesión.</param>
    /// <param name="nivelesDeRoles">
    /// <c>roles.ticket_nivel_creacion</c> de cada rol del usuario (NULL = el rol no define).
    /// </param>
    /// <param name="nivelPerfilUsuario">Nivel del perfil activo del usuario en la empresa, o null.</param>
    public static string NivelEfectivo(
        IEnumerable<string>? permisos,
        IEnumerable<string?>? nivelesDeRoles,
        string? nivelPerfilUsuario)
    {
        if (permisos is not null &&
            permisos.Any(p => PermisosQueImplementan.Contains(p, StringComparer.OrdinalIgnoreCase)))
            return NivelTicket.Implementador;

        if (NivelDeRoles(nivelesDeRoles) == NivelTicket.Implementador)
            return NivelTicket.Implementador;

        if (nivelPerfilUsuario is not null) return nivelPerfilUsuario;

        return NivelTicket.Normal;
    }

    /// <summary>
    /// El mayor nivel que definen los roles, o <c>null</c> si ninguno define (valores basura se
    /// ignoran: fail-closed hacia NORMAL).
    /// </summary>
    public static string? NivelDeRoles(IEnumerable<string?>? nivelesDeRoles)
    {
        if (nivelesDeRoles is null) return null;
        string? mayor = null;
        foreach (var n in nivelesDeRoles)
        {
            var v = NormalizarNivelRol(n);
            if (v == NivelTicket.Implementador) return NivelTicket.Implementador;
            if (v == NivelTicket.Normal) mayor = NivelTicket.Normal;
        }
        return mayor;
    }

    /// <summary>
    /// Normaliza el nivel de un rol: NORMAL / IMPLEMENTADOR en mayúsculas; vacío o desconocido ⇒ null
    /// («el rol no define»).
    /// </summary>
    public static string? NormalizarNivelRol(string? nivel)
    {
        if (string.IsNullOrWhiteSpace(nivel)) return null;
        var v = nivel.Trim().ToUpperInvariant();
        return NivelTicket.Todos.Contains(v) ? v : null;
    }
}
