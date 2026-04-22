// src/ZooSanMarino.API/Controllers/ReporteContableController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Services;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReporteContableController : ControllerBase
{
    private readonly IReporteContableService _reporteContableService;
    private readonly ReporteContableExcelService _excelService;
    private readonly ILogger<ReporteContableController> _logger;

    public ReporteContableController(
        IReporteContableService reporteContableService,
        ReporteContableExcelService excelService,
        ILogger<ReporteContableController> logger)
    {
        _reporteContableService = reporteContableService;
        _excelService = excelService;
        _logger = logger;
    }

    /// <summary>
    /// Genera el reporte contable para un lote padre
    /// </summary>
    [HttpGet("generar")]
    public async Task<ActionResult<ReporteContableCompletoDto>> GenerarReporte(
        [FromQuery] int lotePadreId,
        [FromQuery] string faseLote = "",
        [FromQuery] int? semanaContable = null,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(faseLote) || (faseLote != "Levante" && faseLote != "Produccion"))
                return BadRequest(new { message = "Debe especificar la fase del lote: 'Levante' o 'Produccion'" });

            var request = new GenerarReporteContableRequestDto
            {
                LotePadreId = lotePadreId,
                FaseLote = faseLote,
                SemanaContable = semanaContable,
                FechaInicio = fechaInicio,
                FechaFin = fechaFin
            };

            var reporte = await _reporteContableService.GenerarReporteAsync(request, ct);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al generar reporte contable");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al generar reporte contable: {Message}", ex.Message);
            _logger.LogError(ex, "StackTrace: {StackTrace}", ex.StackTrace);
            if (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException, "InnerException: {Message}", ex.InnerException.Message);
            }
            // En desarrollo, devolver más detalles
            var errorResponse = new { 
                message = "Error interno del servidor", 
                details = ex.Message,
                innerException = ex.InnerException?.Message
            };
            return StatusCode(500, errorResponse);
        }
    }

    /// <summary>
    /// Obtiene las semanas contables disponibles para un lote padre
    /// </summary>
    [HttpGet("semanas-contables/{lotePadreId}")]
    public async Task<ActionResult<List<int>>> ObtenerSemanasContables(
        int lotePadreId,
        CancellationToken ct = default)
    {
        try
        {
            var semanas = await _reporteContableService.ObtenerSemanasContablesAsync(lotePadreId, ct);
            return Ok(semanas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener semanas contables");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Exporta el reporte contable a Excel
    /// </summary>
    [HttpGet("exportar/excel")]
    public async Task<IActionResult> ExportarExcel(
        [FromQuery] int lotePadreId,
        [FromQuery] string faseLote = "",
        [FromQuery] int? semanaContable = null,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(faseLote) || (faseLote != "Levante" && faseLote != "Produccion"))
                return BadRequest(new { message = "Debe especificar la fase del lote: 'Levante' o 'Produccion'" });

            var request = new GenerarReporteContableRequestDto
            {
                LotePadreId = lotePadreId,
                FaseLote = faseLote,
                SemanaContable = semanaContable,
                FechaInicio = fechaInicio,
                FechaFin = fechaFin
            };

            // Generar el reporte
            var reporte = await _reporteContableService.GenerarReporteAsync(request, ct);

            // Generar Excel
            var excelBytes = _excelService.GenerarExcel(reporte);

            // Generar nombre de archivo
            var nombreArchivo = _excelService.GenerarNombreArchivo(reporte, semanaContable);

            // Retornar archivo
            return File(excelBytes, 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                nombreArchivo);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al exportar reporte contable a Excel");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al exportar reporte contable a Excel");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Retorna la jerarquía granjas → núcleos → galpones → lotes base para los filtros del reporte contable
    /// </summary>
    [HttpGet("filtros-disponibles")]
    public async Task<ActionResult<FiltrosContablesDto>> GetFiltrosDisponibles(CancellationToken ct = default)
    {
        try
        {
            var filtros = await _reporteContableService.GetFiltrosDisponiblesAsync(ct);
            return Ok(filtros);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener filtros disponibles para reporte contable");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el reporte de movimientos de huevos para un lote padre
    /// </summary>
    [HttpGet("movimientos-huevos")]
    public async Task<ActionResult<ReporteMovimientosHuevosDto>> ObtenerReporteMovimientosHuevos(
        [FromQuery] int lotePadreId,
        [FromQuery] int? semanaContable = null,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new ObtenerReporteMovimientosHuevosRequestDto
            {
                LotePadreId = lotePadreId,
                SemanaContable = semanaContable,
                FechaInicio = fechaInicio,
                FechaFin = fechaFin
            };

            var reporte = await _reporteContableService.ObtenerReporteMovimientosHuevosAsync(request, ct);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al obtener reporte de movimientos de huevos");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al obtener reporte de movimientos de huevos");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}

