namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Persona "notificada" / copiada en un <see cref="Ticket"/>: no es el solicitante ni el
/// resolutor, pero recibe correo en los hitos clave (creación y cierre) para trazabilidad.
/// </summary>
public class TicketNotificado
{
    public long Id { get; set; }
    public long TicketId { get; set; }

    /// <summary>Guid del usuario notificado (references users.id), si se resolvió como usuario registrado.</summary>
    public Guid? UserGuid { get; set; }

    /// <summary>Cédula/identificación del usuario notificado (fallback, patrón del proyecto).</summary>
    public string? Cedula { get; set; }

    /// <summary>Email al que se envían las notificaciones (requerido: sin email no tiene sentido el registro).</summary>
    public string Email { get; set; } = default!;

    /// <summary>Nombre completo del notificado (para mostrar en la UI y en los correos).</summary>
    public string? Nombre { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Quién agregó a este notificado (de <c>ICurrentUser.UserId</c>).</summary>
    public int CreatedByUserId { get; set; }

    public Ticket? Ticket { get; set; }
}
