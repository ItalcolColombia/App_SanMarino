namespace ZooSanMarino.Application.DTOs.Tickets;

// ───────────────────────── Gestión del caso (tablero admin) ─────────────────────────

/// <summary>Cambia la prioridad del caso.</summary>
public record CambiarPrioridadRequest(string Prioridad);

/// <summary>Reasigna el caso a otro resolutor (gestión directa desde el tablero).</summary>
public record CambiarAsignadoRequest(Guid AsignadoUserGuid, string? Nota = null);

/// <summary>
/// Planificación del caso: fechas del roadmap, compromiso de solución y estimación.
/// Los campos en null se interpretan como "no tocar"; usá los flags para limpiar.
/// </summary>
public record ActualizarPlanificacionRequest(
    DateOnly? FechaInicioPlan = null,
    DateOnly? FechaFinPlan = null,
    DateTime? FechaLimite = null,
    decimal? HorasEstimadas = null,
    bool LimpiarFechaInicioPlan = false,
    bool LimpiarFechaFinPlan = false,
    bool LimpiarFechaLimite = false,
    bool LimpiarHorasEstimadas = false
);

/// <summary>Suelta una tarjeta de CASO en una columna del tablero, en una posición concreta.</summary>
public record MoverTicketRequest(string Estado, int Indice, string? Nota = null);

// ───────────────────────── Tablero ─────────────────────────

/// <summary>Filtros del tablero (sin paginar: el kanban trae todas las columnas a la vez).</summary>
public record TicketTableroFiltro(
    int? Anio = null,
    string? Tipo = null,
    string? Prioridad = null,
    int? PaisId = null,
    int? CompanyId = null,
    Guid? AssignedToGuid = null,
    string? Texto = null,
    /// <summary>Tope de tarjetas por columna (protege el payload). Default 60.</summary>
    int MaxPorColumna = 60
);

public record TicketTableroColumnaDto(
    string Estado,
    string Label,
    int Total,
    IReadOnlyList<TicketListItemDto> Items
);

public record TicketTableroDto(
    IReadOnlyList<TicketTableroColumnaDto> Columnas,
    TicketTableroResumenDto Resumen
);

/// <summary>Indicadores de cabecera del tablero.</summary>
public record TicketTableroResumenDto(
    int Total,
    int Abiertos,
    int EnCurso,
    int Solucionados,
    int Cerrados,
    int Vencidos,
    int PorVencer,
    int SinAsignar,
    decimal HorasRegistradas
);

// ───────────────────────── Roadmap (timeline tipo Jira) ─────────────────────────

public record TicketRoadmapItemDto(
    long Id,
    string? Codigo,
    string Titulo,
    string Tipo,
    string Estado,
    string Prioridad,
    string? AssignedToNombre,
    DateOnly? FechaInicioPlan,
    DateOnly? FechaFinPlan,
    DateTime? FechaLimite,
    DateTime CreatedAt,
    DateTime? FechaSolucion,
    decimal AvanceTareas,
    string EstadoSla,
    IReadOnlyList<TicketRoadmapTareaDto> Tareas
);

public record TicketRoadmapTareaDto(
    long Id,
    string? Codigo,
    string Titulo,
    string Tipo,
    string Estado,
    string? AsignadoNombre,
    DateOnly? FechaInicioPlan,
    DateOnly? FechaFinPlan
);

public record TicketRoadmapDto(
    DateOnly? Desde,
    DateOnly? Hasta,
    IReadOnlyList<TicketRoadmapItemDto> Items
);

// ───────────────────────── Línea de tiempo ─────────────────────────

public record TicketTimelineEventoDto(
    DateTime Momento,
    string Tipo,
    string Titulo,
    string? Detalle,
    string? Autor,
    string? EstadoResultante,
    bool EsInterna,
    long? ReferenciaId
);

// ───────────────────────── Métricas del caso ─────────────────────────

public record TicketMetricasDto(
    double? HorasPrimeraRespuesta,
    double HorasResolucion,
    double? HorasConfirmacionCierre,
    string EstadoSla,
    double? HorasParaVencer,
    decimal AvanceTareas,
    decimal AvanceFlujo,
    int CantidadTareas,
    int TareasListas,
    decimal HorasRegistradas,
    decimal? HorasEstimadas,
    decimal? DesvioHoras,
    IReadOnlyList<TicketPermanenciaEstadoDto> PermanenciaPorEstado
);

public record TicketPermanenciaEstadoDto(string Estado, double Horas);

// ───────────────────────── Solicitante delegado ─────────────────────────

/// <summary>Usuario del sistema candidato a figurar como solicitante de un caso.</summary>
public record SolicitanteCandidatoDto(
    Guid Guid,
    string Nombre,
    string? Email,
    string? Rol,
    string? Empresa,
    string? Cedula
);
