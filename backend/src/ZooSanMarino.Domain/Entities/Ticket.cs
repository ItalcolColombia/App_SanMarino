namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Ticket de soporte / requerimiento. Centraliza errores, dudas, soportes y nuevos
/// requerimientos con trazabilidad por país y empresa (multi-tenant vía
/// <see cref="AuditableEntity"/>). El país y el autor se infieren del contexto del
/// request (<c>ICurrentUser</c>), nunca del body.
/// </summary>
public class Ticket : AuditableEntity
{
    public long Id { get; set; }

    /// <summary>Código legible para soporte (ej: <c>TK-2026-000123</c>). Se genera en backend.</summary>
    public string? Codigo { get; set; }

    /// <summary>País de origen (de <c>ICurrentUser.PaisId</c>).</summary>
    public int PaisId { get; set; }

    /// <summary>SOPORTE | DESARROLLO | REQUERIMIENTO | DUDAS — ver <see cref="TicketTipos"/>.</summary>
    public string Tipo { get; set; } = default!;

    /// <summary>
    /// ABIERTO | EN_ANALISIS | EN_IMPLEMENTACION | SOLUCIONADO | TRANSFERIDO | SUSPENDIDO —
    /// ver <see cref="TicketEstados"/>.
    /// </summary>
    public string Estado { get; set; } = TicketEstados.Abierto;

    public string Titulo { get; set; } = default!;
    public string Descripcion { get; set; } = default!;

    /// <summary>Resolutor (int hash — legacy, conservado para compatibilidad).</summary>
    public int? AssignedToUserId { get; set; }

    /// <summary>Guid real del resolutor asignado (references users.id). Canónico para Iteración 3+.</summary>
    public Guid? AssignedToUserGuid { get; set; }

    /// <summary>Guid real del creador (references users.id).</summary>
    public Guid? CreatedByUserGuid { get; set; }

    /// <summary>Cuándo un resolutor abrió/tomó el ticket por primera vez.</summary>
    public DateTime? FechaPrimeraApertura { get; set; }

    /// <summary>Cuándo pasó a SOLUCIONADO.</summary>
    public DateTime? FechaSolucion { get; set; }

    /// <summary>Descripción de la solución que registra el resolutor al marcar SOLUCIONADO.</summary>
    public string? SolucionDescripcion { get; set; }

    /// <summary>Cuándo el solicitante confirmó el cierre (segunda parte del cierre).</summary>
    public DateTime? FechaCierreSolicitante { get; set; }

    /// <summary>Cédula/identificación del solicitante que confirmó el cierre.</summary>
    public int? CerradoPorUserId { get; set; }

    /// <summary>True si se notificó la solución por correo al solicitante.</summary>
    public bool NotificadoCorreo { get; set; }

    /// <summary>Cuándo se encoló/envió la notificación por correo.</summary>
    public DateTime? FechaNotificacionCorreo { get; set; }

    /// <summary>Email al que se notificó la solución.</summary>
    public string? CorreoNotificadoA { get; set; }

    /// <summary>Estado del registro (A=activo). Patrón del proyecto.</summary>
    public string Status { get; set; } = "A";

    // ─────────────── Solicitante delegado ("a nombre de") ───────────────

    /// <summary>
    /// Usuario del sistema a nombre de quien va el caso, cuando lo registró otra persona
    /// (solo <c>tickets.admin</c> puede delegarlo). Null ⇒ el solicitante es el creador,
    /// que es como se comportaron todos los tickets previos a esta funcionalidad.
    /// </summary>
    public Guid? SolicitanteUserGuid { get; set; }

    /// <summary>Cédula del solicitante delegado (espejo int, para las bandejas por <c>UserId</c>).</summary>
    public int? SolicitanteUserId { get; set; }

    // ─────────────── Gestión tipo tablero (admin) ───────────────

    /// <summary>BAJA | MEDIA | ALTA | CRITICA — ver <see cref="TicketPrioridades"/>.</summary>
    public string Prioridad { get; set; } = TicketPrioridades.Media;

    /// <summary>Posición de la tarjeta dentro de su columna del tablero (0..n-1).</summary>
    public int OrdenTablero { get; set; }

    /// <summary>Estimación de esfuerzo del caso completo.</summary>
    public decimal? HorasEstimadas { get; set; }

    /// <summary>Compromiso de solución. Base del semáforo de SLA; null ⇒ el caso no tiene SLA.</summary>
    public DateTime? FechaLimite { get; set; }

    /// <summary>Fechas planificadas — dibujan la barra del caso en el roadmap.</summary>
    public DateOnly? FechaInicioPlan { get; set; }
    public DateOnly? FechaFinPlan { get; set; }

    /// <summary>
    /// Historia (épica) de ItalJira que agrupa este caso. Null ⇒ el caso está «sin historia», que es
    /// como nacen todos los que registra un usuario final; el área de desarrollo lo mueve a una
    /// historia después. Agrupar un caso NO altera su estado ni su flujo.
    /// </summary>
    public long? HistoriaId { get; set; }

    // Navegación
    public ICollection<TicketImagen> Imagenes { get; set; } = new List<TicketImagen>();
    public ICollection<TicketNota> Notas { get; set; } = new List<TicketNota>();
    public ICollection<TicketAdjunto> Adjuntos { get; set; } = new List<TicketAdjunto>();
    public ICollection<TicketNotificado> Notificados { get; set; } = new List<TicketNotificado>();
    public ICollection<TicketTarea> Tareas { get; set; } = new List<TicketTarea>();
    public ICollection<TicketTiempo> Tiempos { get; set; } = new List<TicketTiempo>();
    public Historia? Historia { get; set; }
}
