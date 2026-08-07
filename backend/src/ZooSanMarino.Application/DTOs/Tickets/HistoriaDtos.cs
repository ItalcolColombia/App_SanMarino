namespace ZooSanMarino.Application.DTOs.Tickets;

// ───────────────────────── Historias (ItalJira) ─────────────────────────

/// <summary>
/// Crea una historia (épica). Empresa, país y autor salen del contexto del request, nunca del body.
/// </summary>
public record CreateHistoriaRequest(
    string Titulo,
    string? Descripcion = null,
    /// <summary>Columna inicial. Null ⇒ BACKLOG.</summary>
    string? Estado = null,
    /// <summary>BAJA|MEDIA|ALTA|CRITICA. Null ⇒ MEDIA.</summary>
    string? Prioridad = null,
    Guid? ResponsableUserGuid = null,
    decimal? HorasEstimadas = null,
    DateOnly? FechaInicioPlan = null,
    DateOnly? FechaFinPlan = null,
    string? Etiquetas = null
);

/// <summary>Edita una historia. Los campos en null NO se tocan (patch parcial explícito).</summary>
public record UpdateHistoriaRequest(
    string? Titulo = null,
    string? Descripcion = null,
    string? Estado = null,
    string? Prioridad = null,
    Guid? ResponsableUserGuid = null,
    decimal? HorasEstimadas = null,
    DateOnly? FechaInicioPlan = null,
    DateOnly? FechaFinPlan = null,
    string? Etiquetas = null,
    /// <summary>True para dejar la historia sin responsable (no se puede expresar con null).</summary>
    bool QuitarResponsable = false
);

/// <summary>Suelta una historia en una columna del tablero, en una posición concreta.</summary>
public record MoverHistoriaRequest(string Estado, int Indice);

/// <summary>
/// Mueve un trabajo existente a una historia (o lo saca, con <c>HistoriaId</c> null).
/// Sirve tanto para un caso creado por un usuario como para una tarea suelta.
/// </summary>
public record AsignarAHistoriaRequest(long? HistoriaId);

/// <summary>Fila de historia para listas, tablero y roadmap.</summary>
public record HistoriaDto(
    long Id,
    string? Codigo,
    string Titulo,
    string? Descripcion,
    string Estado,
    string Prioridad,
    Guid? ResponsableUserGuid,
    string? ResponsableNombre,
    int Orden,
    decimal? HorasEstimadas,
    /// <summary>Suma de las horas imputadas a sus tareas y a los casos que agrupa.</summary>
    decimal HorasRegistradas,
    DateOnly? FechaInicioPlan,
    DateOnly? FechaFinPlan,
    DateTime? FechaInicioReal,
    DateTime? FechaFinReal,
    string? Etiquetas,
    /// <summary>Avance 0..100 derivado de los trabajos vivos (tareas + casos agrupados).</summary>
    int AvancePorcentaje,
    int TrabajosTerminados,
    int TrabajosTotales,
    DateTime CreatedAt,
    string? CreatedByNombre = null,
    /// <summary>Extremos efectivos de la barra del roadmap (propios o derivados de los trabajos).</summary>
    DateOnly? InicioEfectivo = null,
    DateOnly? FinEfectivo = null
);

/// <summary>Historia con el árbol de trabajo que cuelga de ella.</summary>
public record HistoriaDetalleDto(
    HistoriaDto Historia,
    IReadOnlyList<TicketTareaDto> Tareas,
    IReadOnlyList<ItalJiraCasoDto> Casos
);

/// <summary>Caso (ticket) visto desde ItalJira: lo mínimo para pintarlo dentro del árbol.</summary>
public record ItalJiraCasoDto(
    long Id,
    string? Codigo,
    string Titulo,
    string Tipo,
    string Estado,
    string Prioridad,
    Guid? AssignedToUserGuid,
    string? AssignedToNombre,
    long? HistoriaId,
    decimal? HorasEstimadas,
    decimal HorasRegistradas,
    DateOnly? FechaInicioPlan,
    DateOnly? FechaFinPlan,
    DateTime? FechaLimite,
    DateTime CreatedAt,
    int CantidadTareas
);

/// <summary>
/// Backlog completo de ItalJira: las historias con su árbol, más la bandeja «sin historia»
/// (lo que registran los usuarios y las tareas sueltas que todavía no se agruparon).
/// </summary>
public record ItalJiraBacklogDto(
    IReadOnlyList<HistoriaDetalleDto> Historias,
    /// <summary>Casos de usuarios que todavía no pertenecen a ninguna historia.</summary>
    IReadOnlyList<ItalJiraCasoDto> CasosSinHistoria,
    /// <summary>Tareas nacidas en desarrollo que todavía no pertenecen a ninguna historia.</summary>
    IReadOnlyList<TicketTareaDto> TareasSinHistoria,
    ItalJiraResumenDto Resumen
);

/// <summary>Cabecera de indicadores del backlog / panel.</summary>
public record ItalJiraResumenDto(
    int Historias,
    int HistoriasEnCurso,
    int HistoriasListas,
    int Tareas,
    int TareasListas,
    int CasosSinHistoria,
    decimal HorasRegistradas,
    decimal? HorasEstimadas
);

/// <summary>Tablero kanban de historias: una columna por estado.</summary>
public record ItalJiraTableroDto(
    IReadOnlyList<ItalJiraTableroColumnaDto> Columnas,
    ItalJiraResumenDto Resumen
);

public record ItalJiraTableroColumnaDto(string Estado, IReadOnlyList<HistoriaDto> Historias);

/// <summary>Roadmap: una barra por historia con sus trabajos anidados.</summary>
public record ItalJiraRoadmapDto(
    DateOnly? Desde,
    DateOnly? Hasta,
    IReadOnlyList<ItalJiraRoadmapItemDto> Items
);

public record ItalJiraRoadmapItemDto(
    HistoriaDto Historia,
    IReadOnlyList<ItalJiraRoadmapBarraDto> Trabajos
);

/// <summary>Barra de un trabajo dentro de la historia (tarea o caso).</summary>
public record ItalJiraRoadmapBarraDto(
    string Clase,               // TAREA | CASO
    long Id,
    string? Codigo,
    string Titulo,
    string Estado,
    string Prioridad,
    string? ResponsableNombre,
    DateOnly? FechaInicioPlan,
    DateOnly? FechaFinPlan
);

/// <summary>Filtro común de las vistas de ItalJira.</summary>
public record ItalJiraFiltro(
    string? Estado = null,
    string? Prioridad = null,
    Guid? ResponsableUserGuid = null,
    string? Texto = null,
    /// <summary>True para incluir las historias ya en LISTO (por defecto se muestran igual).</summary>
    bool IncluirTerminadas = true
);
