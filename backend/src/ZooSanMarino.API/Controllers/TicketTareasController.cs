using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Tareas de un caso y su registro de tiempos — el tablero tipo Jira.
/// La visibilidad se resuelve en el service con las mismas reglas del caso: quien no puede ver
/// el ticket recibe una lista vacía, nunca datos de casos ajenos.
/// </summary>
/// <remarks>Ninguna ruta contiene <c>admin</c>: AWS WAF (AdminProtection) devuelve 403 a esos paths.</remarks>
[ApiController]
[Route("api/tickets/{ticketId:long}")]
[Produces("application/json")]
public class TicketTareasController : ControllerBase
{
    private readonly ITicketTareaService _service;

    public TicketTareasController(ITicketTareaService service) => _service = service;

    // ───────────────────────────── Tareas ─────────────────────────────

    /// <summary>Tareas vivas del caso, ordenadas por columna y posición del tablero.</summary>
    [HttpGet("tareas")]
    [ProducesResponseType(typeof(IEnumerable<TicketTareaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TicketTareaDto>>> GetTareas(long ticketId, CancellationToken ct)
        => Ok(await _service.GetByTicketAsync(ticketId, ct));

    /// <summary>Crea una tarea. Entra al final de su columna con código correlativo del caso.</summary>
    [HttpPost("tareas")]
    [ProducesResponseType(typeof(TicketTareaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketTareaDto>> CrearTarea(
        long ticketId, [FromBody] CreateTicketTareaRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _service.CreateAsync(ticketId, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Edita una tarea. Los campos ausentes no se tocan (patch parcial).</summary>
    [HttpPut("tareas/{tareaId:long}")]
    [ProducesResponseType(typeof(TicketTareaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketTareaDto>> EditarTarea(
        long ticketId, long tareaId, [FromBody] UpdateTicketTareaRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _service.UpdateAsync(ticketId, tareaId, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Suelta la tarjeta en una columna y posición. Devuelve el tablero recalculado.</summary>
    [HttpPost("tareas/{tareaId:long}/mover")]
    [ProducesResponseType(typeof(IEnumerable<TicketTareaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<TicketTareaDto>>> MoverTarea(
        long ticketId, long tareaId, [FromBody] MoverTicketTareaRequest req, CancellationToken ct)
    {
        try { return Ok(await _service.MoverAsync(ticketId, tareaId, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Eliminación lógica de la tarea.</summary>
    [HttpDelete("tareas/{tareaId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarTarea(long ticketId, long tareaId, CancellationToken ct)
    {
        try { return await _service.DeleteAsync(ticketId, tareaId, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    // ───────────────────────────── Registro de tiempo ─────────────────────────────

    /// <summary>Registros de tiempo del caso (incluye los imputados a sus tareas).</summary>
    [HttpGet("tiempos")]
    [ProducesResponseType(typeof(IEnumerable<TicketTiempoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TicketTiempoDto>>> GetTiempos(long ticketId, CancellationToken ct)
        => Ok(await _service.GetTiemposAsync(ticketId, ct));

    /// <summary>Imputa horas al caso o a una de sus tareas.</summary>
    [HttpPost("tiempos")]
    [ProducesResponseType(typeof(TicketTiempoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketTiempoDto>> RegistrarTiempo(
        long ticketId, [FromBody] CreateTicketTiempoRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _service.AddTiempoAsync(ticketId, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Elimina lógicamente un registro de tiempo (propio, o cualquiera si sos administrador).</summary>
    [HttpDelete("tiempos/{tiempoId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarTiempo(long ticketId, long tiempoId, CancellationToken ct)
    {
        try { return await _service.DeleteTiempoAsync(ticketId, tiempoId, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Totales de horas del caso: registradas, estimadas, desvío y desglose por persona.</summary>
    [HttpGet("tiempos/resumen")]
    [ProducesResponseType(typeof(TicketResumenTiemposDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResumenTiemposDto>> GetResumenTiempos(long ticketId, CancellationToken ct)
    {
        var dto = await _service.GetResumenTiemposAsync(ticketId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }
}
