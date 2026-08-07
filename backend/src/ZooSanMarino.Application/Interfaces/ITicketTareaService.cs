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
}
