// src/ZooSanMarino.API/Controllers/ReporteDiarioCostosEngordeController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.ReporteDiarioCostosEngorde;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Reporte Diario Costos de pollo engorde: por granja (+ lote base opcional) unifica por fecha
/// alimento del día (stock/consumo por tipo), mortalidad+selección y aves vivas por galpón.
/// Datos reales del seguimiento diario vía fn_reporte_diario_costos_engorde.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReporteDiarioCostosEngordeController : ControllerBase
{
    private readonly IReporteDiarioCostosEngordeService _service;
    private readonly ILogger<ReporteDiarioCostosEngordeController> _logger;

    public ReporteDiarioCostosEngordeController(
        IReporteDiarioCostosEngordeService service,
        ILogger<ReporteDiarioCostosEngordeController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Genera el reporte. Sin fechas: arranca en el encaset del lote más reciente del alcance y termina hoy.</summary>
    [HttpPost("generar")]
    [ProducesResponseType(typeof(ReporteDiarioCostosReporteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReporteDiarioCostosReporteDto>> Generar(
        [FromBody] ReporteDiarioCostosRequest request,
        CancellationToken ct)
    {
        if (request is null || request.GranjaId <= 0)
            return BadRequest(new { message = "granjaId es requerido." });
        try
        {
            return Ok(await _service.GenerarAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar el Reporte Diario Costos Engorde. Request: {@Request}", request);
            return StatusCode(500, new { error = "Error interno al generar el reporte diario de costos", message = ex.Message });
        }
    }
}
