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

    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }

    /// <summary>"borrador" | "en_progreso" | "completado" | "cancelado".</summary>
    public string Estado { get; set; } = "borrador";

    public ICollection<ImplementacionTarea> Tareas { get; set; } = new List<ImplementacionTarea>();
}
