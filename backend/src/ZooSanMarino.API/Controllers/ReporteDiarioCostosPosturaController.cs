// src/ZooSanMarino.API/Controllers/ReporteDiarioCostosPosturaController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.ReporteDiarioCostosPostura;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Reporte Diario Área de Costos de POSTURA (levante + producción): por regional/granja/lote base,
/// abre por día y por lote:galpón las bajas de aves (mortalidad, selección, error de sexaje, venta),
/// el alimento consumido por ítem y sexo, y los huevos con sus movimientos.
/// Datos reales del seguimiento diario vía <c>fn_reporte_diario_costos_postura</c>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReporteDiarioCostosPosturaController : ControllerBase
{
    private readonly IReporteDiarioCostosPosturaService _service;
    private readonly ILogger<ReporteDiarioCostosPosturaController> _logger;

    public ReporteDiarioCostosPosturaController(
        IReporteDiarioCostosPosturaService service,
        ILogger<ReporteDiarioCostosPosturaController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Genera el reporte. Todos los filtros son opcionales: sin granja toma todas las asignadas al
    /// usuario, sin fase trae levante y producción, y sin fechas cubre todo el histórico del alcance.
    /// </summary>
    [HttpPost("generar")]
    [ProducesResponseType(typeof(ReporteDiarioCostosPosturaReporteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReporteDiarioCostosPosturaReporteDto>> Generar(
        [FromBody] ReporteDiarioCostosPosturaRequest request,
        CancellationToken ct)
    {
        request ??= new ReporteDiarioCostosPosturaRequest();

        if (request.FechaDesde.HasValue && request.FechaHasta.HasValue
            && request.FechaDesde.Value.Date > request.FechaHasta.Value.Date)
            return BadRequest(new { message = "La fecha inicial no puede ser posterior a la fecha final." });

        try
        {
            return Ok(await _service.GenerarAsync(request, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar el Reporte Diario Costos Postura. Request: {@Request}", request);
            return StatusCode(500, new { error = "Error interno al generar el reporte diario de costos de postura", message = ex.Message });
        }
    }
}
