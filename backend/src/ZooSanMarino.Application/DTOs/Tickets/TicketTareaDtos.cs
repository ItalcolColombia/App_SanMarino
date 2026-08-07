namespace ZooSanMarino.Application.DTOs.Tickets;

// ───────────────────────── Tareas del caso ─────────────────────────

/// <summary>Crea una tarea dentro de un caso. Empresa/autor salen del contexto, no del body.</summary>
public record CreateTicketTareaRequest(
    string Titulo,
    string? Descripcion = null,
    /// <summary>TAREA|HISTORIA|BUG|SUBTAREA|DOCUMENTACION|MEJORA. Null ⇒ TAREA.</summary>
    string? Tipo = null,
    /// <summary>Columna inicial del tablero. Null ⇒ BACKLOG.</summary>
    string? Estado = null,
    /// <summary>BAJA|MEDIA|ALTA|CRITICA. Null ⇒ MEDIA.</summary>
    string? Prioridad = null,
    Guid? AsignadoUserGuid = null,
    long? ParentTareaId = null,
    decimal? HorasEstimadas = null,
    DateOnly? FechaInicioPlan = null,
    DateOnly? FechaFinPlan = null,
    string? Etiquetas = null
);

/// <summary>Edita una tarea. Los campos en null NO se tocan (patch parcial explícito).</summary>
public record UpdateTicketTareaRequest(
    string? Titulo = null,
    string? Descripcion = null,
    string? Tipo = null,
    string? Estado = null,
    string? Prioridad = null,
    Guid? AsignadoUserGuid = null,
    decimal? HorasEstimadas = null,
    DateOnly? FechaInicioPlan = null,
    DateOnly? FechaFinPlan = null,
    string? Etiquetas = null,
    /// <summary>True para dejar la tarea sin responsable (no se puede expresar con null).</summary>
    bool QuitarAsignado = false
);

/// <summary>Suelta una tarjeta en una columna del tablero, en una posición concreta.</summary>
public record MoverTicketTareaRequest(string Estado, int Indice);

public record TicketTareaDto(
    long Id,
    long TicketId,
    string? Codigo,
    string Tipo,
    string Estado,
    string Prioridad,
    string Titulo,
    string? Descripcion,
    Guid? AsignadoUserGuid,
    string? AsignadoNombre,
    long? ParentTareaId,
    int Orden,
    decimal? HorasEstimadas,
    decimal HorasRegistradas,
    DateOnly? FechaInicioPlan,
    DateOnly? FechaFinPlan,
    DateTime? FechaInicioReal,
    DateTime? FechaFinReal,
    string? Etiquetas,
    DateTime CreatedAt,
    string? CreatedByNombre = null,
    /// <summary>Cantidad de subtareas vivas colgando de esta tarea.</summary>
    int CantidadSubtareas = 0
);

// ───────────────────────── Registro de tiempo (worklog) ─────────────────────────

public record CreateTicketTiempoRequest(
    decimal Horas,
    DateOnly? Fecha = null,
    string? Descripcion = null,
    /// <summary>Tarea a la que se imputa. Null = tiempo del caso completo.</summary>
    long? TareaId = null
);

public record TicketTiempoDto(
    long Id,
    long TicketId,
    long? TareaId,
    string? TareaTitulo,
    int UserId,
    Guid? UserGuid,
    string? UserNombre,
    DateOnly Fecha,
    decimal Horas,
    string? Descripcion,
    DateTime CreatedAt
);

/// <summary>Totales de tiempo del caso, para el panel de control de horas.</summary>
public record TicketResumenTiemposDto(
    decimal HorasRegistradas,
    decimal? HorasEstimadas,
    decimal? DesvioHoras,
    IReadOnlyList<TicketTiempoPorPersonaDto> PorPersona
);

public record TicketTiempoPorPersonaDto(Guid? UserGuid, string? Nombre, decimal Horas);
