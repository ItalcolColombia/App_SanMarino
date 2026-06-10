namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Nivel de solicitante de un usuario en el módulo de tickets.
/// NORMAL → puede crear SOPORTE y DUDAS.
/// IMPLEMENTADOR → puede crear SOPORTE, DUDAS, DESARROLLO y REQUERIMIENTO.
/// Un registro por usuario/empresa.
/// </summary>
public class TicketPerfilUsuario
{
    public long Id { get; set; }

    /// <summary>Guid del usuario (references users.id).</summary>
    public Guid UserId { get; set; }

    public int CompanyId { get; set; }

    /// <summary>NORMAL | IMPLEMENTADOR — ver <see cref="NivelTicket"/>.</summary>
    public string Nivel { get; set; } = NivelTicket.Normal;

    public bool Activo { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
