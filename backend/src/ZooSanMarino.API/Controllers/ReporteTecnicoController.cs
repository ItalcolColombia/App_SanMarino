// src/ZooSanMarino.API/Controllers/ReporteTecnicoController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Services;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReporteTecnicoController : ControllerBase
{
    private readonly IReporteTecnicoService _service;
    private readonly ReporteTecnicoExcelService _excelService;
    private readonly ILogger<ReporteTecnicoController> _logger;

    public ReporteTecnicoController(
        IReporteTecnicoService service,
        ReporteTecnicoExcelService excelService,
        ILogger<ReporteTecnicoController> logger)
    {
        _service = service;
        _excelService = excelService;
        _logger = logger;
    }

    /// <summary>
    /// Genera reporte técnico diario para un sublote específico
    /// </summary>
    [HttpGet("diario/sublote/{loteId}")]
    public async Task<ActionResult<ReporteTecnicoCompletoDto>> GetReporteDiarioSublote(
        int loteId,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        try
        {
            var reporte = await _service.GenerarReporteDiarioSubloteAsync(loteId, fechaInicio, fechaFin, ct);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte diario para sublote {LoteId}", loteId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Genera reporte técnico diario consolidado para un lote (todos los sublotes)
    /// </summary>
    [HttpGet("diario/consolidado")]
    public async Task<ActionResult<ReporteTecnicoCompletoDto>> GetReporteDiarioConsolidado(
        [FromQuery] string loteNombre,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        try
        {
            var reporte = await _service.GenerarReporteDiarioConsolidadoAsync(loteNombre, fechaInicio, fechaFin, ct);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte diario consolidado para lote {LoteNombre}", loteNombre);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Genera reporte técnico semanal para un sublote específico
    /// </summary>
    [HttpGet("semanal/sublote/{loteId}")]
    public async Task<ActionResult<ReporteTecnicoCompletoDto>> GetReporteSemanalSublote(
        int loteId,
        [FromQuery] int? semana = null,
        CancellationToken ct = default)
    {
        try
        {
            var reporte = await _service.GenerarReporteSemanalSubloteAsync(loteId, semana, ct);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte semanal para sublote {LoteId}", loteId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Genera reporte técnico semanal consolidado para un lote
    /// Solo consolida semanas completas (7 días) de todos los sublotes
    /// </summary>
    [HttpGet("semanal/consolidado")]
    public async Task<ActionResult<ReporteTecnicoCompletoDto>> GetReporteSemanalConsolidado(
        [FromQuery] string loteNombre,
        [FromQuery] int? semana = null,
        CancellationToken ct = default)
    {
        try
        {
            var reporte = await _service.GenerarReporteSemanalConsolidadoAsync(loteNombre, semana, ct);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte semanal consolidado para lote {LoteNombre}", loteNombre);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Genera reporte técnico según los parámetros de la solicitud
    /// </summary>
    [HttpPost("generar")]
    public async Task<ActionResult<ReporteTecnicoCompletoDto>> GenerarReporte(
        [FromBody] GenerarReporteTecnicoRequestDto request,
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
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte técnico");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene lista de sublotes disponibles para un lote base
    /// </summary>
    [HttpGet("sublotes")]
    public async Task<ActionResult<List<string>>> GetSublotes(
        [FromQuery] string loteNombre,
        CancellationToken ct = default)
    {
        try
        {
            var sublotes = await _service.ObtenerSublotesAsync(loteNombre, ct);
            return Ok(sublotes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener sublotes para lote {LoteNombre}", loteNombre);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Exporta reporte técnico diario a Excel
    /// </summary>
    [HttpPost("exportar/excel/diario")]
    public async Task<IActionResult> ExportarExcelDiario(
        [FromBody] GenerarReporteTecnicoRequestDto request,
        CancellationToken ct = default)
    {
        try
        {
            request.IncluirSemanales = false;
            var reporte = await _service.GenerarReporteAsync(request, ct);
            var excelBytes = _excelService.GenerarExcelDiario(reporte);
            var fileName = _excelService.GenerarNombreArchivo(reporte, "diario");
            
            return File(excelBytes, 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al exportar reporte técnico diario a Excel");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Exporta reporte técnico semanal a Excel
    /// </summary>
    [HttpPost("exportar/excel/semanal")]
    public async Task<IActionResult> ExportarExcelSemanal(
        [FromBody] GenerarReporteTecnicoRequestDto request,
        CancellationToken ct = default)
    {
        try
        {
            request.IncluirSemanales = true;
            var reporte = await _service.GenerarReporteAsync(request, ct);
            var excelBytes = _excelService.GenerarExcelSemanal(reporte);
            var fileName = _excelService.GenerarNombreArchivo(reporte, "semanal");
            
            return File(excelBytes, 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al exportar reporte técnico semanal a Excel");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}

