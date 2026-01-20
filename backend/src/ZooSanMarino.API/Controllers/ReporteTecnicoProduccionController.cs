// src/ZooSanMarino.API/Controllers/ReporteTecnicoProduccionController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Services;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReporteTecnicoProduccionController : ControllerBase
{
    private readonly IReporteTecnicoProduccionService _service;
    private readonly ReporteTecnicoProduccionExcelService _excelService;
    private readonly ILogger<ReporteTecnicoProduccionController> _logger;

    public ReporteTecnicoProduccionController(
        IReporteTecnicoProduccionService service,
        ReporteTecnicoProduccionExcelService excelService,
        ILogger<ReporteTecnicoProduccionController> logger)
    {
        _service = service;
        _excelService = excelService;
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
    /// Genera reporte técnico diario de producción para un lote específico
    /// </summary>
    [HttpGet("diario/{loteId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReporteTecnicoProduccionCompletoDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerarReporteDiario(
        int loteId,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null,
        [FromQuery] bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        try
        {
            var reporte = await _service.GenerarReporteDiarioAsync(
                loteId,
                fechaInicio,
                fechaFin,
                consolidarSublotes,
                ct);
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
            _logger.LogError(ex, "Error al generar reporte diario de producción para lote {LoteId}", loteId);
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

    /// <summary>
    /// Genera reporte técnico "Cuadro" semanal con valores de guía genética (amarillos)
    /// </summary>
    [HttpGet("cuadro/{loteId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReporteTecnicoProduccionCuadroCompletoDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerarReporteCuadro(
        int loteId,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null,
        [FromQuery] bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        try
        {
            var reporte = await _service.GenerarReporteCuadroAsync(
                loteId,
                fechaInicio,
                fechaFin,
                consolidarSublotes,
                ct);
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
            _logger.LogError(ex, "Error al generar reporte cuadro de producción para lote {LoteId}", loteId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Genera reporte de clasificación de huevos comercio semanal con valores de guía genética (amarillos)
    /// </summary>
    [HttpGet("clasificacion-huevo-comercio/{loteId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReporteClasificacionHuevoComercioCompletoDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerarReporteClasificacionHuevoComercio(
        int loteId,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null,
        [FromQuery] bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        try
        {
            var reporte = await _service.GenerarReporteClasificacionHuevoComercioAsync(
                loteId,
                fechaInicio,
                fechaFin,
                consolidarSublotes,
                ct);
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
            _logger.LogError(ex, "Error al generar reporte de clasificación de huevos comercio para lote {LoteId}", loteId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Exporta todos los reportes de producción a Excel (múltiples hojas)
    /// </summary>
    [HttpGet("exportar/excel/{loteId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportarExcelCompleto(
        int loteId,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null,
        [FromQuery] bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        try
        {
            // Generar todos los reportes de forma secuencial para evitar conflictos de DbContext
            // Entity Framework no permite operaciones concurrentes en el mismo DbContext (Scoped)
            var reporteDiario = await _service.GenerarReporteDiarioAsync(
                loteId, fechaInicio, fechaFin, consolidarSublotes, ct);

            var reporteCuadro = await _service.GenerarReporteCuadroAsync(
                loteId, fechaInicio, fechaFin, consolidarSublotes, ct);

            var reporteClasificacion = await _service.GenerarReporteClasificacionHuevoComercioAsync(
                loteId, fechaInicio, fechaFin, consolidarSublotes, ct);

            // Generar Excel con todas las hojas
            var excelBytes = _excelService.GenerarExcelCompleto(
                reporteDiario,
                reporteCuadro,
                reporteClasificacion);

            var fileName = _excelService.GenerarNombreArchivo(reporteDiario.LoteInfo, "completo");

            return File(excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
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
            _logger.LogError(ex, "Error al exportar reportes de producción a Excel para lote {LoteId}", loteId);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}

