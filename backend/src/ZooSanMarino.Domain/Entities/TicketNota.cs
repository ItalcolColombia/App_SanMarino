namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Nota / novedad en la bitácora de un <see cref="Ticket"/> (la deja el creador o el resolutor).
/// Si la nota acompañó un cambio de estado, <see cref="EstadoResultante"/> alimenta la línea de tiempo.
/// </summary>
public class TicketNota
{
    public long Id { get; set; }
    public long TicketId { get; set; }

    /// <summary>Quién dejó la nota (de <c>ICurrentUser.UserId</c>).</summary>
    public int UserId { get; set; }

    public string Nota { get; set; } = default!;

    /// <summary>Estado al que pasó el ticket cuando se registró esta nota (si aplica).</summary>
    public string? EstadoResultante { get; set; }

    /// <summary>Nota interna: visible solo para resolutores / super admin.</summary>
    public bool EsInterna { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket? Ticket { get; set; }
}
