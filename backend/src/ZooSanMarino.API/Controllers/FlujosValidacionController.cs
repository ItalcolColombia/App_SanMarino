// src/ZooSanMarino.API/Controllers/FlujosValidacionController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.FlujosValidacion;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Configuración de flujos de validación parametrizables por empresa: catálogo de procesos,
/// borradores, versionado y publicación (fase_de_desarrollo/flujos_validacion_parametrizables_por_empresa_plan.md §9.4).
///
/// <para>Sin <c>admin</c> en la ruta a propósito: el WAF bloquea ese literal en paths de API.</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FlujosValidacionController : ControllerBase
{
    private readonly IFlujoValidacionService _service;
    private readonly ILogger<FlujosValidacionController> _logger;

    public FlujosValidacionController(IFlujoValidacionService service, ILogger<FlujosValidacionController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("procesos")]
    [ProducesResponseType(typeof(IReadOnlyList<ProcesoValidacionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListarProcesos([FromQuery] int companyId, CancellationToken ct)
    {
        try { return Ok(await _service.ListarProcesosAsync(companyId, ct)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
    }

    [HttpGet]
    [ProducesResponseType(typeof(FlujoValidacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerVigente([FromQuery] int companyId, [FromQuery] string procesoKey, CancellationToken ct)
    {
        try
        {
            var flujo = await _service.ObtenerFlujoVigenteAsync(companyId, procesoKey, ct);
            return flujo is null ? NoContent() : Ok(flujo);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("borradores")]
    [ProducesResponseType(typeof(FlujoValidacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearBorrador([FromBody] CrearBorradorFlujoCommand cmd, CancellationToken ct)
    {
        try { return Ok(await _service.CrearBorradorAsync(cmd, ct)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{flujoId:int}")]
    [ProducesResponseType(typeof(FlujoValidacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ActualizarBorrador(int flujoId, [FromBody] ActualizarBorradorFlujoCommand cmd, CancellationToken ct)
    {
        try { return Ok(await _service.ActualizarBorradorAsync(flujoId, cmd, ct)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{flujoId:int}/clonar")]
    [ProducesResponseType(typeof(FlujoValidacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Clonar(int flujoId, CancellationToken ct)
    {
        try { return Ok(await _service.ClonarAsync(flujoId, ct)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{flujoId:int}/preflight")]
    [ProducesResponseType(typeof(PreflightPublicacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Preflight(int flujoId, CancellationToken ct)
    {
        try { return Ok(await _service.PreflightPublicacionAsync(flujoId, ct)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{flujoId:int}/publicar")]
    [ProducesResponseType(typeof(FlujoValidacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Publicar(int flujoId, CancellationToken ct)
    {
        try
        {
            var flujo = await _service.PublicarAsync(flujoId, ct);
            _logger.LogInformation("Flujo de validación {FlujoId} publicado (empresa {CompanyId}, versión {Version})",
                flujoId, flujo.CompanyId, flujo.Version);
            return Ok(flujo);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{flujoId:int}/retirar")]
    [ProducesResponseType(typeof(FlujoValidacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Retirar(int flujoId, CancellationToken ct)
    {
        try { return Ok(await _service.RetirarAsync(flujoId, ct)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("asignables")]
    [ProducesResponseType(typeof(IReadOnlyList<AsignableFlujoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListarAsignables([FromQuery] int companyId, CancellationToken ct)
    {
        try { return Ok(await _service.ListarAsignablesAsync(companyId, ct)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
    }
}
