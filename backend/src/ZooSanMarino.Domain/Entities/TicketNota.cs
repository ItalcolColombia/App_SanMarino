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

    /// <summary>
    /// Clasifica las notas que escribe el sistema al registrar un cambio de gestión
    /// (ver <see cref="TicketNotaEventos"/>). <c>null</c> = comentario escrito por una persona,
    /// que es lo que son todas las notas anteriores a esta columna.
    /// </summary>
    public string? TipoEvento { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket? Ticket { get; set; }
}

/// <summary>Tipos de nota generada por el sistema, para pintarlas distinto en la línea de tiempo.</summary>
public static class TicketNotaEventos
{
    public const string Asignacion    = "SISTEMA_ASIGNACION";
    public const string Prioridad     = "SISTEMA_PRIORIDAD";
    public const string Tarea         = "SISTEMA_TAREA";
    public const string Planificacion = "SISTEMA_PLANIFICACION";
    public const string Solicitante   = "SISTEMA_SOLICITANTE";
    public const string Tiempo        = "SISTEMA_TIEMPO";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { Asignacion, Prioridad, Tarea, Planificacion, Solicitante, Tiempo };

    /// <summary>True si la nota la escribió el sistema (no una persona).</summary>
    public static bool EsDeSistema(string? tipoEvento) =>
        !string.IsNullOrWhiteSpace(tipoEvento) && Todos.Contains(tipoEvento);
}
