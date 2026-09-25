namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Visita de asistencia técnica programada por un veterinario a una granja asignada. La ubicación
/// puede acotarse progresivamente hasta lote; los ids se conservan como referencia operativa aunque
/// una ubicación sea desactivada después de la visita.
/// </summary>
public class VisitaTecnica : AuditableEntity
{
    public long Id { get; set; }
    public int FarmId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    public int? LoteId { get; set; }

    public string Titulo { get; set; } = null!;
    public string? Objetivo { get; set; }
    public DateTime FechaProgramada { get; set; }
    public DateTime? FechaRealizada { get; set; }
    public string? Observaciones { get; set; }

    /// <summary>PROGRAMADA | REALIZADA | CANCELADA.</summary>
    public string Estado { get; set; } = EstadoVisitaTecnica.Programada;
    public Guid VeterinarioUserId { get; set; }

    public Farm Farm { get; set; } = null!;
    public User VeterinarioUser { get; set; } = null!;
    public ICollection<TareaCampo> Tareas { get; set; } = new List<TareaCampo>();
}

public static class EstadoVisitaTecnica
{
    public const string Programada = "PROGRAMADA";
    public const string Realizada = "REALIZADA";
    public const string Cancelada = "CANCELADA";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Programada, Realizada, Cancelada };
}
