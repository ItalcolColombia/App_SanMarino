namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Perfil de atención de un usuario: qué tipo de ticket atiende y en qué país.
/// <see cref="PaisId"/> NULL = "global" → el resolutor aparece para TODOS los países.
/// Una persona puede tener múltiples registros: ej. (SOPORTE,Ecuador) + (DESARROLLO,global).
/// </summary>
public class TicketResolutor
{
    public long Id { get; set; }

    /// <summary>Guid del usuario resolutor (references users.id).</summary>
    public Guid UserId { get; set; }

    /// <summary>SOPORTE | DESARROLLO | REQUERIMIENTO | DUDAS</summary>
    public string Tipo { get; set; } = default!;

    /// <summary>País que atiende. NULL = global (todos los países).</summary>
    public int? PaisId { get; set; }

    public int CompanyId { get; set; }
    public bool Activo { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
