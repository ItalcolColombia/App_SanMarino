// src/ZooSanMarino.API/Controllers/IndicadorEcuadorController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IndicadorEcuadorController : ControllerBase
{
    private readonly IIndicadorEcuadorService _indicadorService;
    private readonly ILogger<IndicadorEcuadorController> _logger;

    public IndicadorEcuadorController(
        IIndicadorEcuadorService indicadorService,
        ILogger<IndicadorEcuadorController> logger)
    {
        _indicadorService = indicadorService;
        _logger = logger;
    }

    /// <summary>
    /// Calcula indicadores técnicos de Ecuador según filtros
    /// </summary>
    [HttpPost("calcular")]
    public async Task<ActionResult<IEnumerable<IndicadorEcuadorDto>>> CalcularIndicadores(
        [FromBody] IndicadorEcuadorRequest request)
    {
        try
        {
            var resultados = await _indicadorService.CalcularIndicadoresAsync(request);
            return Ok(resultados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al calcular indicadores de Ecuador. Request: {@Request}", request);
            return StatusCode(500, new { 
                error = "Error interno al calcular indicadores",
                message = ex.Message,
                innerException = ex.InnerException?.Message
            });
        }
    }

    /// <summary>
    /// Calcula indicadores consolidados de todas las granjas
    /// </summary>
    [HttpPost("consolidado")]
    public async Task<ActionResult<IndicadorEcuadorConsolidadoDto>> CalcularConsolidado(
        [FromBody] IndicadorEcuadorRequest request)
    {
        try
        {
            var resultado = await _indicadorService.CalcularConsolidadoAsync(request);
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al calcular indicadores consolidados");
            return StatusCode(500, new { error = "Error interno al calcular indicadores consolidados" });
        }
    }

    /// <summary>
    /// Calcula liquidación por período (semanal o mensual)
    /// </summary>
    [HttpPost("liquidacion-periodo")]
    public async Task<ActionResult<LiquidacionPeriodoDto>> CalcularLiquidacionPeriodo(
        [FromBody] LiquidacionPeriodoRequest request)
    {
        try
        {
            var resultado = await _indicadorService.CalcularLiquidacionPeriodoAsync(
                request.FechaInicio,
                request.FechaFin,
                request.TipoPeriodo,
                request.GranjaId);
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al calcular liquidación por período");
            return StatusCode(500, new { error = "Error interno al calcular liquidación por período" });
        }
    }

    /// <summary>
    /// soloCerrados=true (default): lotes con aves=0 cuya fecha de cierre está entre fechaDesde y fechaHasta.
    /// soloCerrados=false: lotes cuyo encaset está en el rango (incluye abiertos).
    /// </summary>
    [HttpGet("lotes-cerrados")]
    public async Task<ActionResult<IEnumerable<IndicadorEcuadorDto>>> ObtenerLotesCerrados(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta,
        [FromQuery] int? granjaId = null,
        [FromQuery] bool soloCerrados = true)
    {
        try
        {
            var resultados = await _indicadorService.ObtenerLotesCerradosAsync(
                fechaDesde,
                fechaHasta,
                granjaId,
                soloCerrados);
            return Ok(resultados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener lotes cerrados");
            return StatusCode(500, new { error = "Error interno al obtener lotes cerrados" });
        }
    }

    /// <summary>
    /// Calcula indicadores de pollo engorde para el lote padre (LoteAveEngorde) y cada lote reproductor asociado.
    /// Tablas: lote_ave_engorde, lote_reproductora_ave_engorde, movimiento_pollo_engorde, seguimiento_diario_aves_engorde, seguimiento_diario_lote_reproductora_aves_engorde.
    /// </summary>
    [HttpPost("indicadores-pollo-engorde-por-lote-padre")]
    public async Task<ActionResult<IndicadorPolloEngordePorLotePadreDto>> IndicadoresPolloEngordePorLotePadre(
        [FromBody] IndicadorPolloEngordePorLotePadreRequest request)
    {
        if (request == null || request.LoteAveEngordeId <= 0)
            return BadRequest(new { error = "LoteAveEngordeId es requerido y debe ser mayor a 0." });
        try
        {
            var resultado = await _indicadorService.CalcularIndicadoresPolloEngordePorLotePadreAsync(request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Indicadores pollo engorde por lote padre: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al calcular indicadores pollo engorde por lote padre");
            return StatusCode(500, new { error = "Error interno al calcular indicadores." });
        }
    }

    /// <summary>
    /// Liquidación técnica Pollo Engorde (solo lote padre liquidado, sin reproductoras).
    /// Modo UnLote: body con loteAveEngordeId. Modo Rango: fechas + alcance (TodasLasGranjas / Granja / Nucleo).
    /// </summary>
    [HttpPost("liquidacion-pollo-engorde-reporte")]
    public async Task<ActionResult<LiquidacionPolloEngordeReporteDto>> LiquidacionPolloEngordeReporte(
        [FromBody] LiquidacionPolloEngordeReporteRequest request,
        CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "Request requerido." });
        try
        {
            var resultado = await _indicadorService.LiquidacionPolloEngordeReporteAsync(request, ct);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Liquidación pollo engorde: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar liquidación pollo engorde");
            return StatusCode(500, new { error = "Error interno al generar el reporte." });
        }
    }
}
