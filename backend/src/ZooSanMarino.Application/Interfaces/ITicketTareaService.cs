using ZooSanMarino.Application.DTOs.Tickets;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Puerto de las tareas de un caso y de su registro de tiempos (el tablero tipo Jira).
/// La visibilidad se resuelve dentro de la implementación con las mismas reglas del caso:
/// quien no puede ver el ticket tampoco ve ni toca sus tareas.
/// </summary>
public interface ITicketTareaService
{
    // ── Tareas ───────────────────────────────────────────────────
    Task<IReadOnlyList<TicketTareaDto>> GetByTicketAsync(long ticketId, CancellationToken ct);
    Task<TicketTareaDto?> CreateAsync(long ticketId, CreateTicketTareaRequest req, CancellationToken ct);
    Task<TicketTareaDto?> UpdateAsync(long ticketId, long tareaId, UpdateTicketTareaRequest req, CancellationToken ct);

    /// <summary>Suelta la tarjeta en una columna del tablero de tareas (drag &amp; drop).</summary>
    Task<IReadOnlyList<TicketTareaDto>> MoverAsync(long ticketId, long tareaId, MoverTicketTareaRequest req, CancellationToken ct);

    /// <summary>Borrado lógico de la tarea (sus registros de tiempo quedan, imputados al caso).</summary>
    Task<bool> DeleteAsync(long ticketId, long tareaId, CancellationToken ct);

    // ── Registro de tiempo (worklog) ─────────────────────────────
    Task<IReadOnlyList<TicketTiempoDto>> GetTiemposAsync(long ticketId, CancellationToken ct);
    Task<TicketTiempoDto?> AddTiempoAsync(long ticketId, CreateTicketTiempoRequest req, CancellationToken ct);
    Task<bool> DeleteTiempoAsync(long ticketId, long tiempoId, CancellationToken ct);
    Task<TicketResumenTiemposDto?> GetResumenTiemposAsync(long ticketId, CancellationToken ct);

    // ── ItalJira: trabajo que NO nace de un caso ──────────────────
    // Mismo escritor de `ticket_tareas` que los métodos de arriba (partial del mismo servicio):
    // las dos vistas comparten proyección, reordenamiento y reglas de fecha.

    /// <summary>Tareas vivas de una historia (incluidas sus subtareas).</summary>
    Task<IReadOnlyList<TicketTareaDto>> GetPorHistoriaAsync(long historiaId, CancellationToken ct);

    /// <summary>Tareas nacidas en desarrollo que todavía no pertenecen a ninguna historia ni caso.</summary>
    Task<IReadOnlyList<TicketTareaDto>> GetSinAgruparAsync(CancellationToken ct);

    /// <summary>
    /// Crea una tarea desde ItalJira: sin caso, colgando de <c>HistoriaId</c> y/o de
    /// <c>ParentTareaId</c> (subtarea/bug). Exige permiso de gestión del módulo.
    /// </summary>
    Task<TicketTareaDto> CrearTareaItalJiraAsync(CreateTicketTareaRequest req, CancellationToken ct);

    /// <summary>Edita una tarea por su id, resolviendo el permiso desde su propio contexto.</summary>
    Task<TicketTareaDto?> ActualizarTareaAsync(long tareaId, UpdateTicketTareaRequest req, CancellationToken ct);

    /// <summary>Mueve una tarea por su id dentro del tablero de su universo (historia o bandeja).</summary>
    Task<IReadOnlyList<TicketTareaDto>> MoverTareaAsync(long tareaId, MoverTicketTareaRequest req, CancellationToken ct);

    /// <summary>Borrado lógico de una tarea por su id.</summary>
    Task<bool> EliminarTareaAsync(long tareaId, CancellationToken ct);

    /// <summary>Imputa horas a una tarea (con o sin caso).</summary>
    Task<TicketTiempoDto?> AddTiempoTareaAsync(long tareaId, CreateTicketTiempoRequest req, CancellationToken ct);
}
