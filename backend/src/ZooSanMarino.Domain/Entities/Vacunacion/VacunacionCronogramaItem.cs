// src/ZooSanMarino.Domain/Entities/Vacunacion/VacunacionCronogramaItem.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Ítem del cronograma de vacunación de un lote: una vacuna programada para aplicarse en una
/// franja de tiempo (semana de vida en Postura, día de vida en Engorde, o fecha fija sin importar
/// la fase). El lote se referencia vía exactamente un FK de línea (<see cref="LotePosturaLevanteId"/>,
/// <see cref="LotePosturaProduccionId"/> o <see cref="LoteAveEngordeId"/>) según <see cref="LineaProductiva"/>,
/// porque no existe un "Lote" único confiable entre líneas (ver plan del módulo).
/// </summary>
public class VacunacionCronogramaItem : AuditableEntity
{
    public int Id { get; set; }
    public int? PaisId { get; set; }

    /// <summary>"Levante" | "Produccion" | "Engorde" (extensible a futuras líneas sin migración de schema).</summary>
    public string LineaProductiva { get; set; } = null!;

    public int? LotePosturaLevanteId { get; set; }
    public int? LotePosturaProduccionId { get; set; }
    public int? LoteAveEngordeId { get; set; }

    // Denormalizado al crear (mismo patrón que las tablas de lote) para filtros de reportes sin join extra.
    public int GranjaId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }

    /// <summary>FK a ItemInventario (filtrado por TipoItem = "vacuna" en el selector).</summary>
    public int ItemInventarioId { get; set; }

    /// <summary>"Semana" | "Dia" | "Fecha".</summary>
    public string UnidadObjetivo { get; set; } = null!;

    /// <summary>Semana N (Postura) o día N de edad (Engorde), según <see cref="UnidadObjetivo"/>.</summary>
    public int? ValorObjetivo { get; set; }

    /// <summary>Fecha fija objetivo, usada solo si <see cref="UnidadObjetivo"/> = "Fecha".</summary>
    public DateTime? FechaObjetivo { get; set; }

    /// <summary>Ancho de la franja válida antes/después del objetivo (ej. semana = 6/0 para lunes-domingo).</summary>
    public int RangoDiasAntes { get; set; }
    public int RangoDiasDespues { get; set; }

    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public string? Notas { get; set; }

    public Farm Farm { get; set; } = null!;
    public Nucleo? Nucleo { get; set; }
    public Galpon? Galpon { get; set; }
    public ItemInventario ItemInventario { get; set; } = null!;
    public LotePosturaLevante? LotePosturaLevante { get; set; }
    public LotePosturaProduccion? LotePosturaProduccion { get; set; }
    public LoteAveEngorde? LoteAveEngorde { get; set; }
    public VacunacionRegistroAplicacion? RegistroAplicacion { get; set; }
}
