namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Actividad operativa dejada por una visita técnica, o creada directamente, para una ubicación.
/// La asignación es territorial: la ven los usuarios con la granja y el alcance correspondiente.
/// </summary>
public class TareaCampo : AuditableEntity
{
    public long Id { get; set; }
    public long? VisitaId { get; set; }
    public int FarmId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    public int? LoteId { get; set; }

    public string Titulo { get; set; } = null!;
    public string? Instrucciones { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public bool RequiereObservacion { get; set; }
    public bool RequiereFoto { get; set; }

    /// <summary>PENDIENTE | REALIZADA | CANCELADA.</summary>
    public string Estado { get; set; } = EstadoTareaCampo.Pendiente;
    public Guid CreadaPorUserId { get; set; }
    public Guid? RealizadaPorUserId { get; set; }
    public DateTime? FechaRealizada { get; set; }
    public string? ObservacionCumplimiento { get; set; }

    public VisitaTecnica? Visita { get; set; }
    public Farm Farm { get; set; } = null!;
    public User CreadaPorUser { get; set; } = null!;
    public User? RealizadaPorUser { get; set; }
    public ICollection<TareaCampoEvidencia> Evidencias { get; set; } = new List<TareaCampoEvidencia>();
}

public static class EstadoTareaCampo
{
    public const string Pendiente = "PENDIENTE";
    public const string Realizada = "REALIZADA";
    public const string Cancelada = "CANCELADA";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Pendiente, Realizada, Cancelada };
}
