// src/ZooSanMarino.Domain/Entities/Implementacion/ImplementacionTareaFirma.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Participante de una tarea del checklist (asistente a la capacitación / receptor de la entrega)
/// y su respuesta: <c>pendiente</c> hasta que responde; <c>firmada</c> cuando digita su firma
/// (<see cref="FirmaTexto"/>) confirmando que estuvo/recibió el punto (con nota opcional);
/// <c>rechazada</c> cuando registra una novedad (motivo en <see cref="Nota"/>) — el front lo guía
/// a crear un ticket. Una fila por (tarea, usuario); quitar un participante es soft-delete y solo
/// se permite mientras siga pendiente (las respuestas son auditoría y no se borran).
/// </summary>
public class ImplementacionTareaFirma : AuditableEntity
{
    public int Id { get; set; }
    public int TareaId { get; set; }

    /// <summary>Guid real de <c>users.id</c> del participante (para joinear nombre y correo).</summary>
    public Guid UserId { get; set; }

    /// <summary>"pendiente" | "firmada" | "rechazada".</summary>
    public string Estado { get; set; } = "pendiente";

    /// <summary>Firma digitada por el participante (texto libre, normalmente su nombre completo).</summary>
    public string? FirmaTexto { get; set; }

    /// <summary>Observación al firmar, o motivo de la novedad al rechazar.</summary>
    public string? Nota { get; set; }

    public DateTime? FechaRespuesta { get; set; }

    public ImplementacionTarea Tarea { get; set; } = null!;
    public User User { get; set; } = null!;
}
