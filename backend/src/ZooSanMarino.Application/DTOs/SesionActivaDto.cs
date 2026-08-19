namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Una sesión, como se la muestra al usuario o al administrador.
///
/// <para>
/// ⚠️ <b>No lleva el <c>jti</c> completo</b>, sólo una etiqueta con sus últimos 8 caracteres: el
/// <c>jti</c> es la llave por la que el servidor identifica el token, y publicarlo en un listado que
/// ve cualquier administrador sería regalar un identificador de sesión ajena.
/// </para>
/// </summary>
public sealed record SesionActivaDto(
    long Id,
    string Etiqueta,
    string? DeviceId,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastSeenAt,
    DateTime? RevokedAt,
    string? RevokedReason,
    /// <summary>¿Es la sesión desde la que se está mirando? La UI avisa antes de que se cierre a sí mismo.</summary>
    bool EsLaActual);

/// <summary>Body de la revocación. El id de la sesión va en la ruta, nunca en el body.</summary>
public sealed record RevocarSesionRequest(string? Motivo);
