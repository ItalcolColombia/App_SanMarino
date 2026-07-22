// src/ZooSanMarino.API/Controllers/ImplementacionController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Planes de implementación por empresa: cronogramas de entrega de la aplicación con checklist
/// de doble check (el gestor completa, el usuario asignado confirma desde "Mis tareas").
/// Empresa/país activos resueltos por headers (X-Active-Company-Id / X-Active-Pais).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
[Tags("Implementación — Cronogramas de entrega")]
public class ImplementacionController : ControllerBase
{
    private readonly IImplementacionService _svc;

    public ImplementacionController(IImplementacionService svc)
    {
        _svc = svc;
    }

    // ── Planes ────────────────────────────────────────────────────────────────

    [HttpGet("planes")]
    [ProducesResponseType(typeof(List<ImplementacionPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlanes(CancellationToken ct)
        => Ok(await _svc.GetPlanesAsync(ct));

    [HttpGet("planes/{id:int}")]
    [ProducesResponseType(typeof(ImplementacionPlanDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanDetalle(int id, CancellationToken ct)
    {
        var dto = await _svc.GetPlanDetalleAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("planes")]
    [ProducesResponseType(typeof(ImplementacionPlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePlan([FromBody] ImplementacionPlanCreateRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.CreatePlanAsync(req, ct);
            return CreatedAtAction(nameof(GetPlanDetalle), new { id = dto.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("planes/{id:int}")]
    [ProducesResponseType(typeof(ImplementacionPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePlan(int id, [FromBody] ImplementacionPlanUpdateRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.UpdatePlanAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("planes/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePlan(int id, CancellationToken ct)
        => await _svc.DeletePlanAsync(id, ct) ? NoContent() : NotFound();

    // ── Tareas del checklist ─────────────────────────────────────────────────

    [HttpPost("planes/{id:int}/tareas")]
    [ProducesResponseType(typeof(ImplementacionTareaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTarea(int id, [FromBody] ImplementacionTareaCreateRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.CreateTareaAsync(id, req, ct);
            return dto is null ? NotFound() : CreatedAtAction(nameof(GetPlanDetalle), new { id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("tareas/{id:int}")]
    [ProducesResponseType(typeof(ImplementacionTareaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTarea(int id, [FromBody] ImplementacionTareaUpdateRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.UpdateTareaAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("tareas/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTarea(int id, CancellationToken ct)
        => await _svc.DeleteTareaAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Check del gestor: marca la tarea completada con fecha y usuario.</summary>
    [HttpPost("tareas/{id:int}/completar")]
    [ProducesResponseType(typeof(ImplementacionTareaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompletarTarea(int id, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.CompletarTareaAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Confirmación del usuario asignado (403 si quien llama no es el asignado).</summary>
    [HttpPost("tareas/{id:int}/confirmar")]
    [ProducesResponseType(typeof(ImplementacionTareaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConfirmarTarea(int id, [FromBody] ImplementacionConfirmarRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.ConfirmarTareaAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Reabre una tarea (deshace check y confirmación) para corregir errores.</summary>
    [HttpPost("tareas/{id:int}/reabrir")]
    [ProducesResponseType(typeof(ImplementacionTareaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReabrirTarea(int id, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.ReabrirTareaAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Participantes y firmas ───────────────────────────────────────────────

    /// <summary>
    /// Sincroniza los participantes (asistentes) de la tarea que deben firmar el recibido.
    /// Solo se pueden quitar participantes que sigan pendientes.
    /// </summary>
    [HttpPut("tareas/{id:int}/participantes")]
    [ProducesResponseType(typeof(ImplementacionTareaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetParticipantes(int id, [FromBody] ImplementacionParticipantesRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.SetParticipantesAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Firma digitada del participante actual: confirma que estuvo/recibió el punto (404 si no es participante).</summary>
    [HttpPost("tareas/{id:int}/firmar")]
    [ProducesResponseType(typeof(ImplementacionMiFirmaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Firmar(int id, [FromBody] ImplementacionFirmarRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.FirmarAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Novedad del participante actual: registra el motivo por el que NO firma (el front lo guía a crear un ticket).</summary>
    [HttpPost("tareas/{id:int}/rechazar")]
    [ProducesResponseType(typeof(ImplementacionMiFirmaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Rechazar(int id, [FromBody] ImplementacionRechazarRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.RechazarAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Puntos donde el usuario actual es participante (pendientes de firma + historial).</summary>
    [HttpGet("mis-firmas")]
    [ProducesResponseType(typeof(List<ImplementacionMiFirmaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMisFirmas(CancellationToken ct)
        => Ok(await _svc.GetMisFirmasAsync(ct));

    // ── Consultas de apoyo ───────────────────────────────────────────────────

    /// <summary>Tareas asignadas al usuario actual en la empresa activa (vista "Mis tareas").</summary>
    [HttpGet("mis-tareas")]
    [ProducesResponseType(typeof(List<ImplementacionMiTareaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMisTareas(CancellationToken ct)
        => Ok(await _svc.GetMisTareasAsync(ct));

    /// <summary>Usuarios activos de la empresa activa, para el combo de asignación.</summary>
    [HttpGet("usuarios-asignables")]
    [ProducesResponseType(typeof(List<ImplementacionUsuarioAsignableDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsuariosAsignables(CancellationToken ct)
        => Ok(await _svc.GetUsuariosAsignablesAsync(ct));

    /// <summary>Roles asociados a la empresa activa, para el combo de rol responsable.</summary>
    [HttpGet("roles-asignables")]
    [ProducesResponseType(typeof(List<ImplementacionRolAsignableDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRolesAsignables(CancellationToken ct)
        => Ok(await _svc.GetRolesAsignablesAsync(ct));
}
