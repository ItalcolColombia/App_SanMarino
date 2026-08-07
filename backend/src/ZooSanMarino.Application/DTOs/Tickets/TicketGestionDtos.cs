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

/// <summary>
/// Filtros compartidos por el tablero, el roadmap, el panel de indicadores y el reporte.
/// Sin paginar: el kanban trae todas las columnas a la vez.
/// </summary>
public record TicketTableroFiltro(
    int? Anio = null,
    string? Tipo = null,
    string? Prioridad = null,
    int? PaisId = null,
    int? CompanyId = null,
    Guid? AssignedToGuid = null,
    string? Texto = null,
    /// <summary>Tope de tarjetas por columna (protege el payload). Default 60.</summary>
    int MaxPorColumna = 60,
    /// <summary>Selección múltiple de países. Tiene prioridad sobre <see cref="PaisId"/>.</summary>
    IReadOnlyList<int>? PaisIds = null,
    /// <summary>Selección múltiple de empresas. Tiene prioridad sobre <see cref="CompanyId"/>.</summary>
    IReadOnlyList<int>? CompanyIds = null,
    /// <summary>Rango de creación. Más fino que <see cref="Anio"/>; si viene, manda.</summary>
    DateTime? Desde = null,
    DateTime? Hasta = null,
    /// <summary>Filtra por estado del caso (columna del tablero).</summary>
    string? Estado = null,
    /// <summary>SIN_SLA | EN_TIEMPO | POR_VENCER | VENCIDO | CUMPLIDO | INCUMPLIDO.</summary>
    string? EstadoSla = null
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

// ───────────────────────── Panel de indicadores ─────────────────────────

/// <summary>Cabecera del panel: volumen, efectividad y tiempos promedio del conjunto filtrado.</summary>
public record TicketResumenIndicadoresDto(
    int Total,
    int Abiertos,
    int EnCurso,
    int Solucionados,
    int Cerrados,
    int Suspendidos,
    int Vencidos,
    int PorVencer,
    int SinAsignar,
    int TareasTotal,
    int TareasListas,
    int TareasPendientes,
    decimal HorasEstimadas,
    decimal HorasRegistradas,
    /// <summary>Horas promedio hasta que el equipo tomó el caso. Null si ninguno fue tomado.</summary>
    double? PromedioPrimeraRespuesta,
    /// <summary>Horas promedio hasta la solución, sobre los casos ya solucionados.</summary>
    double? PromedioResolucion,
    double? PromedioConfirmacionCierre,
    /// <summary>% de casos con compromiso que se cumplieron. Null si ninguno tiene fecha límite.</summary>
    decimal? Efectividad,
    decimal PorcentajeResueltos,
    decimal AvanceTareas,
    int ConCompromiso,
    int CompromisoCumplido
);

/// <summary>Desglose de un agrupador con identidad: se usa igual para país y para empresa.</summary>
public record TicketIndicadorGrupoDto(
    int Id, string Nombre, int Total, int Abiertos, int EnCurso, int Resueltos,
    int Vencidos, decimal HorasRegistradas, decimal AvanceTareas,
    double? PromedioResolucion, decimal? Efectividad
);

public record TicketIndicadorCategoriaDto(
    string Clave, string Label, int Total, int Resueltos, int Vencidos, double? PromedioResolucion
);

public record TicketIndicadorResponsableDto(
    Guid? Guid, string Nombre, int Asignados, int Resueltos, int Vencidos,
    decimal HorasRegistradas, int TareasListas, double? PromedioResolucion
);

public record TicketIndicadoresDto(
    TicketResumenIndicadoresDto Resumen,
    IReadOnlyList<TicketIndicadorGrupoDto> PorPais,
    IReadOnlyList<TicketIndicadorGrupoDto> PorEmpresa,
    IReadOnlyList<TicketIndicadorCategoriaDto> PorEstado,
    IReadOnlyList<TicketIndicadorCategoriaDto> PorTipo,
    IReadOnlyList<TicketIndicadorCategoriaDto> PorPrioridad,
    IReadOnlyList<TicketIndicadorResponsableDto> PorResponsable
);

// ───────────────────────── Reporte detallado (Excel) ─────────────────────────

/// <summary>Una fila de la hoja «Casos»: todo lo que hace falta para auditar el caso.</summary>
public record TicketReporteCasoDto(
    long Id, string? Codigo, string? Pais, string? Empresa, string Tipo, string Estado,
    string Prioridad, string Titulo,
    string? Solicitante, string? SolicitanteEmail, string? RegistradoPor, string? Responsable,
    DateTime CreatedAt, DateTime? PrimeraApertura, DateTime? FechaSolucion, DateTime? FechaCierre,
    DateTime? FechaLimite, string EstadoSla,
    double? HorasPrimeraRespuesta, double HorasResolucion,
    DateOnly? FechaInicioPlan, DateOnly? FechaFinPlan,
    decimal? HorasEstimadas, decimal HorasRegistradas, decimal? DesvioHoras,
    int TareasTotal, int TareasListas, decimal AvanceTareas,
    string? SolucionDescripcion
);

/// <summary>Una fila de la hoja «Tareas».</summary>
public record TicketReporteTareaDto(
    string? CodigoCaso, string? TituloCaso, string? Pais,
    string? Codigo, string Tipo, string Estado, string Prioridad, string Titulo,
    string? Responsable, decimal? HorasEstimadas, decimal HorasRegistradas,
    DateOnly? FechaInicioPlan, DateOnly? FechaFinPlan,
    DateTime? FechaInicioReal, DateTime? FechaFinReal, DateTime CreatedAt
);

/// <summary>Una fila de la hoja «Tiempos» (worklog).</summary>
public record TicketReporteTiempoDto(
    string? CodigoCaso, string? TituloCaso, string? Pais, string? Tarea,
    string? Persona, DateOnly Fecha, decimal Horas, string? Descripcion
);

/// <summary>Todo el reporte en un solo viaje: el frontend arma el .xlsx multi-hoja.</summary>
public record TicketReporteDto(
    TicketIndicadoresDto Indicadores,
    IReadOnlyList<TicketReporteCasoDto> Casos,
    IReadOnlyList<TicketReporteTareaDto> Tareas,
    IReadOnlyList<TicketReporteTiempoDto> Tiempos,
    /// <summary>Descripción legible de los filtros aplicados, para el encabezado del Excel.</summary>
    IReadOnlyList<string> FiltrosAplicados
);

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
