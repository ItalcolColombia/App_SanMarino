using ZooSanMarino.Application.DTOs.Tickets;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Puerto de ItalJira: las historias (épicas) y las vistas que las agregan — backlog, tablero y
/// roadmap. La visibilidad se resuelve dentro de la implementación: ItalJira es el módulo del área
/// de desarrollo, así que exige permiso de gestión (<c>tickets.gestionar</c>) o de administración
/// (<c>tickets.admin</c>) — los mismos que hoy protegen tablero, roadmap y panel.
/// </summary>
public interface IHistoriaService
{
    // ── Permiso ───────────────────────────────────────────────────────────

    /// <summary>
    /// ¿El usuario actual puede gestionar ItalJira? Es la MISMA regla que aplican los métodos de
    /// escritura de este servicio (y la que espeja <c>ITicketTareaService</c>), expuesta para que
    /// un llamador de otro módulo pueda exigirla ANTES de decidir si tiene trabajo que delegar.
    ///
    /// <para>
    /// Existe por un defecto real: <c>ImplementacionService.SincronizarConItalJiraAsync</c> apoyaba
    /// su permiso en que los servicios de ItalJira lanzaran al crear. Cuando el plan ya estaba
    /// enlazado no había nada que crear, nadie miraba el permiso y un usuario sin
    /// <c>tickets.gestionar</c> recibía 200 —y le sellaba <c>updated_by</c> al plan—. La misma
    /// llamada, con el mismo usuario, contestaba distinto según el estado de los datos.
    /// </para>
    /// </summary>
    bool PuedeGestionarItalJira();

    // ── CRUD ──────────────────────────────────────────────────────────────
    Task<IReadOnlyList<HistoriaDto>> GetAllAsync(ItalJiraFiltro filtro, CancellationToken ct);
    Task<HistoriaDetalleDto?> GetByIdAsync(long id, CancellationToken ct);
    Task<HistoriaDto> CreateAsync(CreateHistoriaRequest req, CancellationToken ct);
    Task<HistoriaDto?> UpdateAsync(long id, UpdateHistoriaRequest req, CancellationToken ct);

    /// <summary>Suelta la historia en una columna del tablero (drag &amp; drop).</summary>
    Task<IReadOnlyList<HistoriaDto>> MoverAsync(long id, MoverHistoriaRequest req, CancellationToken ct);

    /// <summary>
    /// Borrado lógico. Las tareas y los casos que agrupaba NO se borran: quedan «sin historia».
    /// </summary>
    Task<bool> DeleteAsync(long id, CancellationToken ct);

    // ── Agrupar trabajo existente ─────────────────────────────────────────

    /// <summary>Mueve un caso a una historia (o lo saca con <c>HistoriaId</c> null). No toca su estado.</summary>
    Task<bool> AsignarCasoAsync(long ticketId, AsignarAHistoriaRequest req, CancellationToken ct);

    /// <summary>Mueve una tarea a una historia (o la saca con <c>HistoriaId</c> null).</summary>
    Task<bool> AsignarTareaAsync(long tareaId, AsignarAHistoriaRequest req, CancellationToken ct);

    // ── Vistas agregadas ──────────────────────────────────────────────────
    Task<ItalJiraBacklogDto> GetBacklogAsync(ItalJiraFiltro filtro, CancellationToken ct);
    Task<ItalJiraTableroDto> GetTableroAsync(ItalJiraFiltro filtro, CancellationToken ct);
    Task<ItalJiraRoadmapDto> GetRoadmapAsync(ItalJiraFiltro filtro, CancellationToken ct);
}
