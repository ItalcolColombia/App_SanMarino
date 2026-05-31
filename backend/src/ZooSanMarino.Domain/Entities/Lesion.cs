namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Registro de lesión observada en un lote. Tab "Lesiones" dentro del módulo de
/// Seguimiento Diario (Reproductora / Apoyo / Engorde) — solo visible en Panamá.
/// Una sola tabla cubre los tres módulos vía la columna <see cref="ModuloOrigen"/>.
/// </summary>
public class Lesion : AuditableEntity
{
    public long Id { get; set; }

    // Ubicación productiva
    public int? ClienteId { get; set; }
    public int FarmId { get; set; }
    public string? GalponId { get; set; }
    public int? LoteId { get; set; }
    public string? LoteReproductoraId { get; set; }

    // Información del lote / lesión
    public int? EdadDias { get; set; }
    public int? AvesMacho { get; set; }
    public int? AvesHembra { get; set; }
    public int? AvesMixtas { get; set; }
    public string TipoLesion { get; set; } = default!;
    public string? Observaciones { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    /// <summary>'REPRODUCTORA' | 'APOYO' | 'ENGORDE'</summary>
    public string ModuloOrigen { get; set; } = default!;

    public string Status { get; set; } = "A";
}
