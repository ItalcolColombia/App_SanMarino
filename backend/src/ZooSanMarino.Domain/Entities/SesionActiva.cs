namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Una sesión de usuario emitida por el login. Es una <b>lista blanca</b>: el JWT sólo vale mientras
/// existe su fila, no está revocada y no venció — sin fila no hay sesión (fail-closed).
///
/// <para>
/// Es lo que hace revocable a un token que antes no lo era: perder una tablet, cambiar la contraseña
/// o desactivar a un usuario apagan la sesión con un <c>UPDATE</c>, sin esperar a que el token venza.
/// La decisión de si sigue viva es pura y vive en <c>RevocacionSesionCalculos</c>.
/// </para>
///
/// <para>
/// Los PAT (<c>sk_…</c>) quedan fuera: tienen su propia revocación en <see cref="ServiceToken"/>.
/// </para>
/// </summary>
public class SesionActiva
{
    public long Id { get; set; }

    /// <summary>Claim <c>jti</c> del JWT. Único: es la llave por la que se busca en cada request.</summary>
    public Guid Jti { get; set; }

    /// <summary>Dueño de la sesión. Sale del token ya validado, nunca de un body.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Identificador del dispositivo (<c>X-Device-Id</c>), si el cliente lo mandó. Es lo que permite
    /// decir «esta es la tablet del galpón 3» al listar las sesiones de alguien.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>IP del login. Auditoría: junto al user-agent es lo que identifica una sesión ajena.</summary>
    public string? IpAddress { get; set; }

    /// <summary>User-agent del login (recortado). Auditoría.</summary>
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Mismo instante que el <c>exp</c> del JWT.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Último contacto real con el servidor. Lo escribe el heartbeat con throttle de 5 min:
    /// un <c>UPDATE</c> por request sería peor que el <c>SELECT</c> que la caché evita.
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    /// <summary>Momento de revocación (null = activa). No existe «des-revocar»: se vuelve a entrar.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Quién la revocó (null si fue el sistema: cambio de contraseña, baja del usuario).</summary>
    public Guid? RevokedByUserId { get; set; }

    /// <summary>Motivo, para la auditoría de la revocación.</summary>
    public string? RevokedReason { get; set; }
}
