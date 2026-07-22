// src/ZooSanMarino.Domain/Entities/Implementacion/ImplementacionPlan.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Plan (cronograma) de implementación de la aplicación para una empresa: agrupa el checklist de
/// tareas de entrega (parametrizaciones, capacitaciones, carga de datos, puesta en marcha…).
/// El estado se deriva de las tareas (<c>borrador·en_progreso·completado</c>) salvo <c>cancelado</c>,
/// que se fija manualmente y se respeta.
/// </summary>
public class ImplementacionPlan : AuditableEntity
{
    public int Id { get; set; }
    public int? PaisId { get; set; }

    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }

    /// <summary>"implementacion" | "capacitacion" | "mixto" (entrega + capacitación).</summary>
    public string Tipo { get; set; } = "implementacion";

    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }

    /// <summary>"borrador" | "en_progreso" | "completado" | "cancelado".</summary>
    public string Estado { get; set; } = "borrador";

    /// <summary>
    /// Encargado/implementador responsable de la entrega (Guid real de <c>users.id</c>).
    /// Si al crear no se elige un "implementador diferente", queda el mismo creador.
    /// </summary>
    public Guid? ImplementadorUserId { get; set; }

    /// <summary>Guid real del creador (la auditoría int heredada no permite joinear nombre/correo).</summary>
    public Guid? CreadoPorUserGuid { get; set; }

    public ICollection<ImplementacionTarea> Tareas { get; set; } = new List<ImplementacionTarea>();

    public User? ImplementadorUser { get; set; }
    public User? CreadoPorUser { get; set; }
}
