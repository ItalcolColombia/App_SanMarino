namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Historia (épica) del módulo ItalJira: el nivel de arriba de la jerarquía de trabajo del área de
/// desarrollo. Agrupa <see cref="TicketTarea"/> (que a su vez tienen subtareas/bugs por
/// <c>ParentTareaId</c>) y también <see cref="Ticket"/> completos cuando un caso de un usuario
/// pertenece a la misma línea de trabajo.
/// </summary>
/// <remarks>
/// Una historia se crea SIEMPRE a mano desde ItalJira (área de desarrollo / requerimientos /
/// administrador); nunca la origina un usuario final. Lo que crea un usuario nace como caso o tarea
/// SIN historia y se arrastra dentro de una después.
///
/// Comparte vocabulario con las tareas a propósito: usa <see cref="TicketTareaEstados"/> y
/// <see cref="TicketPrioridades"/> para que el tablero tenga las mismas columnas en los dos niveles.
/// La historia NO registra horas propias: las agrega de sus tareas y casos (evita el doble conteo).
/// </remarks>
public class Historia : AuditableEntity
{
    public long Id { get; set; }

    /// <summary>Código legible (ej: <c>HIS-2026-0001</c>). Se genera en backend.</summary>
    public string? Codigo { get; set; }

    /// <summary>País de origen (de <c>ICurrentUser.PaisId</c>), nunca del body.</summary>
    public int PaisId { get; set; }

    public string Titulo { get; set; } = default!;
    public string? Descripcion { get; set; }

    /// <summary>BACKLOG | ANALISIS | DOCUMENTACION | EN_CURSO | EN_REVISION | LISTO | BLOQUEADA.</summary>
    public string Estado { get; set; } = TicketTareaEstados.Backlog;

    /// <summary>BAJA | MEDIA | ALTA | CRITICA — ver <see cref="TicketPrioridades"/>.</summary>
    public string Prioridad { get; set; } = TicketPrioridades.Media;

    /// <summary>Guid del responsable de la historia (references users.id). Null = sin asignar.</summary>
    public Guid? ResponsableUserGuid { get; set; }

    /// <summary>Posición dentro de su columna del tablero (0..n-1, sin huecos).</summary>
    public int Orden { get; set; }

    /// <summary>Estimación del esfuerzo total de la historia.</summary>
    public decimal? HorasEstimadas { get; set; }

    /// <summary>Fechas planificadas — dibujan la barra en el roadmap.</summary>
    public DateOnly? FechaInicioPlan { get; set; }
    public DateOnly? FechaFinPlan { get; set; }

    /// <summary>Cuándo entró por primera vez a EN_CURSO.</summary>
    public DateTime? FechaInicioReal { get; set; }

    /// <summary>Cuándo pasó a LISTO.</summary>
    public DateTime? FechaFinReal { get; set; }

    /// <summary>Etiquetas libres separadas por coma (ej: <c>postura,backend</c>).</summary>
    public string? Etiquetas { get; set; }

    // Navegación
    public ICollection<TicketTarea> Tareas { get; set; } = new List<TicketTarea>();
    public ICollection<Ticket> Casos { get; set; } = new List<Ticket>();
}

/// <summary>
/// Columnas de la historia. Alias explícito de <see cref="TicketTareaEstados"/>: los dos niveles
/// comparten vocabulario para que el tablero no tenga que traducir estados entre filas.
/// </summary>
public static class HistoriaEstados
{
    public const string Backlog       = TicketTareaEstados.Backlog;
    public const string Analisis      = TicketTareaEstados.Analisis;
    public const string Documentacion = TicketTareaEstados.Documentacion;
    public const string EnCurso       = TicketTareaEstados.EnCurso;
    public const string EnRevision    = TicketTareaEstados.EnRevision;
    public const string Listo         = TicketTareaEstados.Listo;
    public const string Bloqueada     = TicketTareaEstados.Bloqueada;

    /// <summary>Columnas en orden de izquierda a derecha.</summary>
    public static string[] Columnas => TicketTareaEstados.Columnas;

    public static bool EsValido(string? estado) => TicketTareaEstados.EsValido(estado);

    /// <summary>True si la historia cuenta como terminada.</summary>
    public static bool EsTerminal(string? estado) => TicketTareaEstados.EsTerminal(estado);
}
