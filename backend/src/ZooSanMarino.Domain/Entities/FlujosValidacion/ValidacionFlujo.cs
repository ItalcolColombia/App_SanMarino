namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Definición versionada de una secuencia de validación para una empresa + proceso. Solo una
/// versión puede estar <see cref="EstadoFlujoValidacion.Publicado"/> a la vez por empresa/proceso
/// (índice único parcial). Publicado = inmutable: cambios van por clon a versión N+1.
/// </summary>
public class ValidacionFlujo
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public int ProcesoId { get; set; }

    /// <summary>Consecutivo por empresa/proceso, arranca en 1.</summary>
    public int Version { get; set; }

    public string Nombre { get; set; } = null!;

    /// <summary>BORRADOR | PUBLICADO | RETIRADO — ver <see cref="EstadoFlujoValidacion"/>.</summary>
    public string Estado { get; set; } = EstadoFlujoValidacion.Borrador;

    public int PlazoTotalHoras { get; set; } = 24;
    public bool RequierePersonasDistintas { get; set; } = true;
    public bool PermiteAprobacionCreador { get; set; } = false;

    public Guid CreatedByUserId { get; set; }
    public Guid? PublishedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? RetiredAt { get; set; }

    public Company Company { get; set; } = null!;
    public ValidacionProceso Proceso { get; set; } = null!;
    public ICollection<ValidacionFlujoPaso> Pasos { get; set; } = new List<ValidacionFlujoPaso>();
    public ICollection<ValidacionInstancia> Instancias { get; set; } = new List<ValidacionInstancia>();
}

public static class EstadoFlujoValidacion
{
    public const string Borrador = "BORRADOR";
    public const string Publicado = "PUBLICADO";
    public const string Retirado = "RETIRADO";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Borrador, Publicado, Retirado };

    public static bool EsValido(string? estado) =>
        !string.IsNullOrWhiteSpace(estado) && Todos.Contains(estado);
}
