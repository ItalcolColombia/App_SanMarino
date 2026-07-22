// src/ZooSanMarino.Domain/Entities/Implementacion/ImplementacionTarea.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Tarea del checklist de un plan de implementación. Doble check para auditar la entrega:
/// (1) el gestor la marca <c>completada</c> (queda fecha + usuario) y (2) el usuario asignado
/// (<see cref="AsignadoUserId"/>, Guid real de <c>users.id</c>) la <c>confirma</c> desde
/// "Mis tareas" (queda fecha + usuario). Los actores se guardan como Guid para poder joinear
/// nombres y validar identidad, a diferencia de la auditoría int heredada de AuditableEntity.
/// </summary>
public class ImplementacionTarea : AuditableEntity
{
    public int Id { get; set; }
    public int PlanId { get; set; }

    /// <summary>Agrupador del cronograma, ej. "Parametrizaciones", "Capacitación".</summary>
    public string Categoria { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }

    public DateTime? FechaProgramada { get; set; }

    /// <summary>Rol responsable (opcional, informativo).</summary>
    public int? RoleId { get; set; }

    /// <summary>Usuario que debe confirmar el cumplimiento (opcional).</summary>
    public Guid? AsignadoUserId { get; set; }

    /// <summary>"pendiente" | "completada" | "confirmada".</summary>
    public string Estado { get; set; } = "pendiente";

    public DateTime? FechaCompletada { get; set; }
    public Guid? CompletadaPorUserId { get; set; }

    public DateTime? FechaConfirmada { get; set; }
    public Guid? ConfirmadaPorUserId { get; set; }

    public string? Observaciones { get; set; }

    public ImplementacionPlan Plan { get; set; } = null!;
    public Role? Role { get; set; }
    public User? AsignadoUser { get; set; }
    public User? CompletadaPorUser { get; set; }
    public User? ConfirmadaPorUser { get; set; }

    /// <summary>Participantes que deben firmar el recibido de este punto (asistentes).</summary>
    public ICollection<ImplementacionTareaFirma> Firmas { get; set; } = new List<ImplementacionTareaFirma>();
}
