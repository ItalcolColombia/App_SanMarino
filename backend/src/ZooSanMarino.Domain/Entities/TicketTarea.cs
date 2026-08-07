namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Tarea / historia de trabajo dentro de un <see cref="Ticket"/> (el ticket es el CASO).
/// Es la unidad que se arrastra en el tablero del administrador: tiene su propio estado,
/// prioridad, responsable, estimación y planificación.
/// </summary>
/// <remarks>
/// Las subtareas se modelan con <see cref="ParentTareaId"/> apuntando a otra tarea del mismo caso.
/// El caso conserva su propia máquina de estados (<see cref="TicketEstados"/>): mover tareas NO
/// cambia el estado del caso — esa decisión sigue siendo del resolutor.
///
/// Desde ItalJira una tarea también puede nacer SIN caso (trabajo que arranca en el área de
/// desarrollo): en ese escenario <see cref="TicketId"/> es null y la tarea cuelga de una
/// <see cref="Historia"/> o de otra tarea. El invariante es que al menos uno de
/// <see cref="TicketId"/> / <see cref="HistoriaId"/> / <see cref="ParentTareaId"/> tenga valor.
/// </remarks>
public class TicketTarea : AuditableEntity
{
    public long Id { get; set; }

    /// <summary>Caso al que pertenece. Null = tarea nacida en desarrollo (ItalJira), sin ticket.</summary>
    public long? TicketId { get; set; }

    /// <summary>Historia (épica) que agrupa la tarea. Null = tarea sin historia.</summary>
    public long? HistoriaId { get; set; }

    /// <summary>Código legible correlativo dentro del caso (ej: <c>TK-2026-000123-T3</c>).</summary>
    public string? Codigo { get; set; }

    /// <summary>TAREA | HISTORIA | BUG | SUBTAREA | DOCUMENTACION | MEJORA — ver <see cref="TicketTareaTipos"/>.</summary>
    public string Tipo { get; set; } = TicketTareaTipos.Tarea;

    /// <summary>BACKLOG | ANALISIS | DOCUMENTACION | EN_CURSO | EN_REVISION | LISTO | BLOQUEADA.</summary>
    public string Estado { get; set; } = TicketTareaEstados.Backlog;

    /// <summary>BAJA | MEDIA | ALTA | CRITICA — ver <see cref="TicketPrioridades"/>.</summary>
    public string Prioridad { get; set; } = TicketPrioridades.Media;

    public string Titulo { get; set; } = default!;
    public string? Descripcion { get; set; }

    /// <summary>Guid del responsable (references users.id). Null = sin asignar.</summary>
    public Guid? AsignadoUserGuid { get; set; }

    /// <summary>Tarea padre cuando esta es una subtarea. Null en las tareas de primer nivel.</summary>
    public long? ParentTareaId { get; set; }

    /// <summary>Posición dentro de su columna del tablero (0..n-1, sin huecos).</summary>
    public int Orden { get; set; }

    public decimal? HorasEstimadas { get; set; }

    /// <summary>Fechas planificadas — dibujan la barra en el roadmap.</summary>
    public DateOnly? FechaInicioPlan { get; set; }
    public DateOnly? FechaFinPlan { get; set; }

    /// <summary>Cuándo entró por primera vez a EN_CURSO.</summary>
    public DateTime? FechaInicioReal { get; set; }

    /// <summary>Cuándo pasó a LISTO.</summary>
    public DateTime? FechaFinReal { get; set; }

    /// <summary>Etiquetas libres separadas por coma (ej: <c>backend,urgente</c>).</summary>
    public string? Etiquetas { get; set; }

    // Navegación
    public Ticket? Ticket { get; set; }
    public Historia? Historia { get; set; }
    public ICollection<TicketTiempo> Tiempos { get; set; } = new List<TicketTiempo>();
}

/// <summary>Tipos de tarea. Persistidos como string (patrón del módulo, no enum mapeado a BD).</summary>
public static class TicketTareaTipos
{
    public const string Tarea         = "TAREA";
    public const string Historia      = "HISTORIA";
    public const string Bug           = "BUG";
    public const string Subtarea      = "SUBTAREA";
    public const string Documentacion = "DOCUMENTACION";
    public const string Mejora        = "MEJORA";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { Tarea, Historia, Bug, Subtarea, Documentacion, Mejora };

    public static bool EsValido(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) && Todos.Contains(tipo);
}

/// <summary>
/// Columnas del tablero de tareas. A diferencia del caso, una tarea se mueve a CUALQUIER
/// columna (es un tablero, no un flujo con compuertas): la única regla es que el estado exista.
/// </summary>
public static class TicketTareaEstados
{
    public const string Backlog       = "BACKLOG";
    public const string Analisis      = "ANALISIS";
    public const string Documentacion = "DOCUMENTACION";
    public const string EnCurso       = "EN_CURSO";
    public const string EnRevision    = "EN_REVISION";
    public const string Listo         = "LISTO";
    public const string Bloqueada     = "BLOQUEADA";

    /// <summary>Columnas en orden de izquierda a derecha.</summary>
    public static readonly string[] Columnas =
        { Backlog, Analisis, Documentacion, EnCurso, EnRevision, Listo, Bloqueada };

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Backlog, Analisis, Documentacion, EnCurso, EnRevision, Listo, Bloqueada };

    public static bool EsValido(string? estado) =>
        !string.IsNullOrWhiteSpace(estado) && Todos.Contains(estado);

    /// <summary>True si la tarea cuenta como terminada para el % de avance del caso.</summary>
    public static bool EsTerminal(string? estado) =>
        Listo.Equals(estado, StringComparison.OrdinalIgnoreCase);
}
