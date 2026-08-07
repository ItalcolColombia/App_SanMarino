using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// ItalJira — gestión del área de desarrollo: historias (épicas), las tareas que cuelgan de ellas y
/// las vistas que las agregan (backlog, tablero y roadmap).
/// </summary>
/// <remarks>
/// La puerta es el PERMISO del módulo (<c>tickets.gestionar</c> / <c>tickets.admin</c>), resuelto
/// dentro del service: quien no lo tiene recibe listas vacías, nunca datos ajenos.
///
/// Ninguna ruta contiene <c>admin</c>: AWS WAF (AdminProtection) devuelve 403 a cualquier path de
/// API con esa palabra — incidente ya documentado en el repo.
/// </remarks>
[ApiController]
[Route("api/italjira")]
[Produces("application/json")]
public class ItalJiraController : ControllerBase
{
    private readonly IHistoriaService _historias;
    private readonly ITicketTareaService _tareas;

    public ItalJiraController(IHistoriaService historias, ITicketTareaService tareas)
    {
        _historias = historias;
        _tareas = tareas;
    }

    // ───────────────────────────── Historias ─────────────────────────────

    /// <summary>Historias vivas con su avance y sus horas agregadas.</summary>
    [HttpGet("historias")]
    [ProducesResponseType(typeof(IEnumerable<HistoriaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<HistoriaDto>>> GetHistorias(
        [FromQuery] string? estado, [FromQuery] string? prioridad,
        [FromQuery] Guid? responsable, [FromQuery] string? texto,
        [FromQuery] bool incluirTerminadas = true, CancellationToken ct = default)
        => Ok(await _historias.GetAllAsync(
            new ItalJiraFiltro(estado, prioridad, responsable, texto, incluirTerminadas), ct));

    /// <summary>Historia con el árbol de trabajo que cuelga de ella (tareas y casos agrupados).</summary>
    [HttpGet("historias/{id:long}")]
    [ProducesResponseType(typeof(HistoriaDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HistoriaDetalleDto>> GetHistoria(long id, CancellationToken ct)
    {
        var dto = await _historias.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Crea una historia. El código correlativo (<c>HIS-AAAA-NNNN</c>) lo genera el backend.</summary>
    [HttpPost("historias")]
    [ProducesResponseType(typeof(HistoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HistoriaDto>> CrearHistoria(
        [FromBody] CreateHistoriaRequest req, CancellationToken ct)
    {
        try { return Ok(await _historias.CreateAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Edita una historia. Los campos ausentes no se tocan (patch parcial).</summary>
    [HttpPut("historias/{id:long}")]
    [ProducesResponseType(typeof(HistoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HistoriaDto>> EditarHistoria(
        long id, [FromBody] UpdateHistoriaRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _historias.UpdateAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Suelta la historia en una columna del tablero. Devuelve el tablero recalculado.</summary>
    [HttpPost("historias/{id:long}/mover")]
    [ProducesResponseType(typeof(IEnumerable<HistoriaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<HistoriaDto>>> MoverHistoria(
        long id, [FromBody] MoverHistoriaRequest req, CancellationToken ct)
    {
        try { return Ok(await _historias.MoverAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Borra la historia. Su trabajo NO se borra: vuelve a la bandeja «sin historia».</summary>
    [HttpDelete("historias/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarHistoria(long id, CancellationToken ct)
    {
        try { return await _historias.DeleteAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    // ───────────────────────────── Agrupar trabajo existente ─────────────────────────────

    /// <summary>Mueve un caso a una historia (o lo saca con <c>historiaId</c> null). No toca su estado.</summary>
    [HttpPut("casos/{ticketId:long}/historia")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AsignarCaso(
        long ticketId, [FromBody] AsignarAHistoriaRequest req, CancellationToken ct)
    {
        try { return await _historias.AsignarCasoAsync(ticketId, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Mueve una tarea a una historia (o la saca). Sus subtareas viajan con ella.</summary>
    [HttpPut("tareas/{tareaId:long}/historia")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AsignarTarea(
        long tareaId, [FromBody] AsignarAHistoriaRequest req, CancellationToken ct)
    {
        try { return await _historias.AsignarTareaAsync(tareaId, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    // ───────────────────────────── Tareas de ItalJira ─────────────────────────────

    /// <summary>Tareas vivas de una historia (incluidas sus subtareas y bugs).</summary>
    [HttpGet("historias/{historiaId:long}/tareas")]
    [ProducesResponseType(typeof(IEnumerable<TicketTareaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TicketTareaDto>>> GetTareasDeHistoria(
        long historiaId, CancellationToken ct)
        => Ok(await _tareas.GetPorHistoriaAsync(historiaId, ct));

    /// <summary>Tareas nacidas en desarrollo que todavía no pertenecen a ninguna historia.</summary>
    [HttpGet("tareas/sin-historia")]
    [ProducesResponseType(typeof(IEnumerable<TicketTareaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TicketTareaDto>>> GetTareasSinHistoria(CancellationToken ct)
        => Ok(await _tareas.GetSinAgruparAsync(ct));

    /// <summary>
    /// Crea una tarea desde ItalJira (sin caso): dentro de una historia con <c>historiaId</c>, o
    /// como subtarea/bug de otra tarea con <c>parentTareaId</c>.
    /// </summary>
    [HttpPost("tareas")]
    [ProducesResponseType(typeof(TicketTareaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketTareaDto>> CrearTarea(
        [FromBody] CreateTicketTareaRequest req, CancellationToken ct)
    {
        try { return Ok(await _tareas.CrearTareaItalJiraAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Edita una tarea por su id (tenga caso o no).</summary>
    [HttpPut("tareas/{tareaId:long}")]
    [ProducesResponseType(typeof(TicketTareaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketTareaDto>> EditarTarea(
        long tareaId, [FromBody] UpdateTicketTareaRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _tareas.ActualizarTareaAsync(tareaId, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Suelta la tarjeta en una columna dentro de su universo (historia o bandeja).</summary>
    [HttpPost("tareas/{tareaId:long}/mover")]
    [ProducesResponseType(typeof(IEnumerable<TicketTareaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<TicketTareaDto>>> MoverTarea(
        long tareaId, [FromBody] MoverTicketTareaRequest req, CancellationToken ct)
    {
        try { return Ok(await _tareas.MoverTareaAsync(tareaId, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Eliminación lógica de una tarea por su id.</summary>
    [HttpDelete("tareas/{tareaId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarTarea(long tareaId, CancellationToken ct)
    {
        try { return await _tareas.EliminarTareaAsync(tareaId, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Imputa horas a una tarea (tenga caso o no).</summary>
    [HttpPost("tareas/{tareaId:long}/tiempos")]
    [ProducesResponseType(typeof(TicketTiempoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketTiempoDto>> RegistrarTiempo(
        long tareaId, [FromBody] CreateTicketTiempoRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _tareas.AddTiempoTareaAsync(tareaId, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    // ───────────────────────────── Vistas agregadas ─────────────────────────────

    /// <summary>
    /// Backlog completo: historias con su árbol + la bandeja «sin historia» (lo que registran los
    /// usuarios y las tareas sueltas todavía sin agrupar).
    /// </summary>
    [HttpGet("backlog")]
    [ProducesResponseType(typeof(ItalJiraBacklogDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ItalJiraBacklogDto>> GetBacklog(
        [FromQuery] string? estado, [FromQuery] string? prioridad,
        [FromQuery] Guid? responsable, [FromQuery] string? texto,
        [FromQuery] bool incluirTerminadas = true, CancellationToken ct = default)
        => Ok(await _historias.GetBacklogAsync(
            new ItalJiraFiltro(estado, prioridad, responsable, texto, incluirTerminadas), ct));

    /// <summary>Tablero kanban de historias, una columna por estado.</summary>
    [HttpGet("tablero")]
    [ProducesResponseType(typeof(ItalJiraTableroDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ItalJiraTableroDto>> GetTablero(
        [FromQuery] string? estado, [FromQuery] string? prioridad,
        [FromQuery] Guid? responsable, [FromQuery] string? texto,
        [FromQuery] bool incluirTerminadas = true, CancellationToken ct = default)
        => Ok(await _historias.GetTableroAsync(
            new ItalJiraFiltro(estado, prioridad, responsable, texto, incluirTerminadas), ct));

    /// <summary>Roadmap: una barra por historia con sus trabajos anidados.</summary>
    [HttpGet("roadmap")]
    [ProducesResponseType(typeof(ItalJiraRoadmapDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ItalJiraRoadmapDto>> GetRoadmap(
        [FromQuery] string? estado, [FromQuery] string? prioridad,
        [FromQuery] Guid? responsable, [FromQuery] string? texto,
        [FromQuery] bool incluirTerminadas = true, CancellationToken ct = default)
        => Ok(await _historias.GetRoadmapAsync(
            new ItalJiraFiltro(estado, prioridad, responsable, texto, incluirTerminadas), ct));
}
