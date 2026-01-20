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
    /// Soporta consolidación por lote padre (loteId) o por nombre base (loteNombre)
    /// </summary>
    [HttpGet("diario/consolidado")]
    public async Task<ActionResult<ReporteTecnicoCompletoDto>> GetReporteDiarioConsolidado(
        [FromQuery] string? loteNombre = null,
        [FromQuery] int? loteId = null,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        try
        {
            var reporte = await _service.GenerarReporteDiarioConsolidadoAsync(
                loteNombre ?? string.Empty, 
                fechaInicio, 
                fechaFin, 
                loteId, 
                ct);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte diario consolidado para lote {LoteNombre} o {LoteId}", loteNombre, loteId);
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
    /// Soporta consolidación por lote padre (loteId) o por nombre base (loteNombre)
    /// </summary>
    [HttpGet("semanal/consolidado")]
    public async Task<ActionResult<ReporteTecnicoCompletoDto>> GetReporteSemanalConsolidado(
        [FromQuery] string? loteNombre = null,
        [FromQuery] int? loteId = null,
        [FromQuery] int? semana = null,
        CancellationToken ct = default)
    {
        try
        {
            var reporte = await _service.GenerarReporteSemanalConsolidadoAsync(
                loteNombre ?? string.Empty, 
                semana, 
                loteId, 
                ct);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte semanal consolidado para lote {LoteNombre} o {LoteId}", loteNombre, loteId);
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
    /// Soporta búsqueda por loteId (nueva lógica de lote padre) o por loteNombre (compatibilidad)
    /// </summary>
    [HttpGet("sublotes")]
    public async Task<ActionResult<List<string>>> GetSublotes(
        [FromQuery] string? loteNombre = null,
        [FromQuery] int? loteId = null,
        CancellationToken ct = default)
    {
        try
        {
            var sublotes = await _service.ObtenerSublotesAsync(loteNombre ?? string.Empty, loteId, ct);
            return Ok(sublotes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener sublotes para lote {LoteNombre} o {LoteId}", loteNombre, loteId);
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

    /// <summary>
    /// Genera reporte técnico completo de Levante con estructura Excel (25 semanas)
    /// Incluye todos los campos calculados, manuales y de guía según estructura Excel
    /// </summary>
    [HttpGet("levante/completo/{loteId}")]
    public async Task<ActionResult<ReporteTecnicoLevanteCompletoDto>> GetReporteLevanteCompleto(
        int loteId,
        [FromQuery] bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        try
        {
            var reporte = await _service.GenerarReporteLevanteCompletoAsync(loteId, consolidarSublotes, ct);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte técnico completo de Levante para lote {LoteId}", loteId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Genera reporte técnico de Levante con estructura de tabs
    /// Incluye datos diarios separados (machos y hembras) y datos semanales completos
    /// </summary>
    [HttpGet("levante/tabs/{loteId}")]
    public async Task<ActionResult<ReporteTecnicoLevanteConTabsDto>> GetReporteLevanteConTabs(
        int loteId,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null,
        [FromQuery] bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        try
        {
            var reporte = await _service.GenerarReporteLevanteConTabsAsync(
                loteId, 
                fechaInicio, 
                fechaFin, 
                consolidarSublotes, 
                ct);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte técnico con tabs para lote {LoteId}", loteId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Exporta todos los reportes de Levante a Excel (múltiples hojas)
    /// Hoja 1: Diario Hembras, Hoja 2: Diario Machos, Hoja 3: Semanal Hembras, Hoja 4: Semanal Machos
    /// </summary>
    [HttpGet("levante/exportar/excel/{loteId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportarExcelCompletoLevante(
        int loteId,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null,
        [FromQuery] bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        try
        {
            // Generar reporte con tabs de forma secuencial para evitar conflictos de DbContext
            var reporte = await _service.GenerarReporteLevanteConTabsAsync(
                loteId, fechaInicio, fechaFin, consolidarSublotes, ct);

            // Generar Excel con todas las hojas
            var excelBytes = _excelService.GenerarExcelCompletoLevante(reporte);
            var fileName = _excelService.GenerarNombreArchivoLevante(reporte, "completo");

            return File(excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al exportar reportes de Levante a Excel para lote {LoteId}", loteId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}

