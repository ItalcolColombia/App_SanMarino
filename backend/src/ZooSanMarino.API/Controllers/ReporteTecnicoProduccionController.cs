// src/ZooSanMarino.API/Controllers/ReporteTecnicoProduccionController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReporteTecnicoProduccionController : ControllerBase
{
    private readonly IReporteTecnicoProduccionService _service;
    private readonly ILogger<ReporteTecnicoProduccionController> _logger;

    public ReporteTecnicoProduccionController(
        IReporteTecnicoProduccionService service,
        ILogger<ReporteTecnicoProduccionController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Genera reporte técnico de producción (diario o semanal, por sublote o consolidado)
    /// </summary>
    [HttpPost("generar")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReporteTecnicoProduccionCompletoDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarReporte(
        [FromBody] GenerarReporteTecnicoProduccionRequestDto request,
        CancellationToken ct = default)
    {
        try
        {
            var reporte = await _service.GenerarReporteAsync(request, ct);
            return Ok(reporte);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte técnico de producción");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene la lista de sublotes para un lote base
    /// </summary>
    [HttpGet("sublotes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
    public async Task<IActionResult> ObtenerSublotes(
        [FromQuery] string loteNombreBase,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(loteNombreBase))
                return BadRequest(new { message = "loteNombreBase es requerido" });

            var sublotes = await _service.ObtenerSublotesAsync(loteNombreBase, ct);
            return Ok(sublotes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener sublotes para {LoteNombreBase}", loteNombreBase);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}

