namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Registro de trabajo (worklog) sobre un caso o una de sus tareas: cuántas horas dedicó
/// quién y en qué día. Es la base del control de tiempos del módulo de administración.
/// </summary>
/// <remarks>
/// <see cref="TareaId"/> null significa que el tiempo se imputó al caso completo (no a una tarea
/// puntual). El borrado es lógico (<c>DeletedAt</c>) para no alterar totales ya reportados.
///
/// <see cref="TicketId"/> null es el caso simétrico de ItalJira: horas imputadas a una tarea que
/// nació en desarrollo y no tiene caso. Al menos uno de los dos identifica el trabajo.
/// </remarks>
public class TicketTiempo
{
    public long Id { get; set; }

    /// <summary>Caso al que se imputa. Null = la tarea no pertenece a un caso (trabajo de ItalJira).</summary>
    public long? TicketId { get; set; }

    /// <summary>Tarea a la que se imputa. Null = tiempo del caso completo.</summary>
    public long? TareaId { get; set; }

    /// <summary>Guid de quien registró el tiempo (references users.id).</summary>
    public Guid? UserGuid { get; set; }

    /// <summary>Cédula de quien registró (espejo int, patrón del módulo).</summary>
    public int UserId { get; set; }

    /// <summary>Día al que corresponde el trabajo (fecha pura, sin hora).</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Horas dedicadas. Positivo, con dos decimales (ej: 1,50 = una hora y media).</summary>
    public decimal Horas { get; set; }

    public string? Descripcion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    // Navegación
    public Ticket? Ticket { get; set; }
    public TicketTarea? Tarea { get; set; }
}
