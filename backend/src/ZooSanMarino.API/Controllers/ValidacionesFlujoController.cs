// src/ZooSanMarino.API/Controllers/ValidacionesFlujoController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.FlujosValidacion;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Ejecución de instancias del motor de flujos de validación: aprobar, devolver, corregir/reenviar,
/// eliminar, bandeja personal y novedades del Home
/// (fase_de_desarrollo/flujos_validacion_parametrizables_por_empresa_plan.md §9.4).
///
/// <para>Sin <c>admin</c> en la ruta a propósito: el WAF bloquea ese literal en paths de API.</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ValidacionesFlujoController : ControllerBase
{
    private readonly IFlujoValidacionService _service;
    private readonly ILogger<ValidacionesFlujoController> _logger;

    public ValidacionesFlujoController(IFlujoValidacionService service, ILogger<ValidacionesFlujoController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("{procesoKey}/{recursoId}")]
    [ProducesResponseType(typeof(EstadoInstanciaFlujoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerEstado(string procesoKey, string recursoId, CancellationToken ct)
    {
        try
        {
            var estado = await _service.ObtenerEstadoAsync(procesoKey, recursoId, ct);
            return estado is null ? NoContent() : Ok(estado);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{instanciaId:guid}/aprobar")]
    [ProducesResponseType(typeof(ResultadoAccionInstanciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Aprobar(Guid instanciaId, CancellationToken ct)
    {
        try
        {
            var resultado = await _service.AprobarAsync(new AprobarInstanciaCommand(instanciaId), ct);
            if (resultado.Finalizada)
                _logger.LogInformation("Instancia de flujo {InstanciaId} finalizada (última etapa aprobada)", instanciaId);
            return Ok(resultado);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Devuelve para corrección. Motivo obligatorio; retrocede exactamente una etapa.</summary>
    [HttpPost("{instanciaId:guid}/devolver")]
    [ProducesResponseType(typeof(ResultadoAccionInstanciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Devolver(Guid instanciaId, [FromBody] DevolverBody body, CancellationToken ct)
    {
        try { return Ok(await _service.DevolverAsync(new DevolverInstanciaCommand(instanciaId, body.Motivo), ct)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Mueve el flujo de vuelta a la etapa que devolvió, tras editar el registro por el camino normal
    /// (esta llamada NO edita datos: los cambios ya se guardaron antes por el CRUD del módulo).
    /// </summary>
    [HttpPost("{instanciaId:guid}/corregir-y-reenviar")]
    [ProducesResponseType(typeof(ResultadoAccionInstanciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CorregirYReenviar(Guid instanciaId, CancellationToken ct)
    {
        try { return Ok(await _service.CorregirYReenviarAsync(new CorregirYReenviarInstanciaCommand(instanciaId), ct)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{instanciaId:guid}/recurso")]
    [ProducesResponseType(typeof(ResultadoAccionInstanciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Eliminar(Guid instanciaId, CancellationToken ct)
    {
        try { return Ok(await _service.EliminarAsync(new EliminarInstanciaCommand(instanciaId), ct)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("mis-pendientes")]
    [ProducesResponseType(typeof(IReadOnlyList<PendienteFlujoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MisPendientes([FromQuery] int companyId, [FromQuery] string? procesoKey, CancellationToken ct)
    {
        try { return Ok(await _service.ObtenerMisPendientesAsync(companyId, procesoKey, ct)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
    }

    /// <summary>Avisos activos del Home para el usuario/empresa actual (tarjeta roja persistente).</summary>
    [HttpGet("mis-novedades")]
    [ProducesResponseType(typeof(IReadOnlyList<NovedadValidacionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MisNovedades(CancellationToken ct) => Ok(await _service.ObtenerMisNovedadesAsync(ct));

    /// <summary>Marca la novedad como leída. NO la resuelve ni la oculta del panel.</summary>
    [HttpPost("novedades/{novedadId:long}/marcar-leida")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarcarNovedadLeida(long novedadId, CancellationToken ct)
    {
        await _service.MarcarNovedadLeidaAsync(novedadId, ct);
        return NoContent();
    }

    public record DevolverBody(string Motivo);
}
