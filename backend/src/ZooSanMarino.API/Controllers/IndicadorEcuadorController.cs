// src/ZooSanMarino.API/Controllers/IndicadorEcuadorController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.API.Services;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IndicadorEcuadorController : ControllerBase
{
    private readonly IIndicadorEcuadorService _indicadorService;
    private readonly ICurrentUser _current;
    private readonly ILogger<IndicadorEcuadorController> _logger;

    public IndicadorEcuadorController(
        IIndicadorEcuadorService indicadorService,
        ICurrentUser current,
        ILogger<IndicadorEcuadorController> logger)
    {
        _indicadorService = indicadorService;
        _current = current;
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

    /// <summary>
    /// Verificador de liquidación: sube el Excel "correcto" (formato vertical etiqueta|valor del TOTAL
    /// de la corrida) y devuelve el análisis armado por la BD: reconciliación sistema vs Excel,
    /// hallazgos (registros con falla, p.ej. despachos sin peso) y simulación de corrección.
    /// El back solo parsea el Excel y delega en fn_auditoria_liquidacion_engorde.
    /// </summary>
    [HttpPost("auditoria-liquidacion")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> AuditoriaLiquidacion(
        [FromForm] int granjaId,
        [FromForm] string? nucleoId,
        [FromForm] string? loteCodigo,
        IFormFile? excel,
        CancellationToken ct)
    {
        if (excel is null || excel.Length == 0)
            return BadRequest(new { error = "Debe adjuntar el archivo Excel en el campo 'excel'." });
        if (granjaId <= 0)
            return BadRequest(new { error = "granjaId es obligatorio." });

        try
        {
            using var ms = new MemoryStream();
            await excel.CopyToAsync(ms, ct);
            ms.Position = 0;
            var valores = AuditoriaLiquidacionExcelParser.Parse(ms);

            if (valores.Count == 0)
                return BadRequest(new { error = "No se reconocieron indicadores en el Excel. Verifique el formato (columna de etiqueta + columna de valor)." });

            var json = await _indicadorService.AuditarLiquidacionAsync(
                new AuditoriaLiquidacionRequest(granjaId, nucleoId, loteCodigo), valores, ct);

            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en auditoría de liquidación. granja={Granja} nucleo={Nucleo} lote={Lote}",
                granjaId, nucleoId, loteCodigo);
            return StatusCode(500, new { error = "Error interno al auditar la liquidación.", message = ex.Message });
        }
    }

    /// <summary>
    /// Aplica la corrección sugerida por el verificador: carga el peso faltante (KgTotal, distribuido
    /// por aves) en los despachos sin peso de la corrida. Requiere el permiso
    /// 'liquidacion.aplicar_correccion'. Escribe en movimiento_pollo_engorde (auditado).
    /// </summary>
    [HttpPost("auditoria-liquidacion/aplicar")]
    public async Task<IActionResult> AplicarCorreccionLiquidacion(
        [FromBody] AplicarCorreccionRequest request,
        CancellationToken ct)
    {
        if (!_current.Permissions.Contains("liquidacion.aplicar_correccion"))
            return Forbid();
        if (request is null || request.GranjaId <= 0)
            return BadRequest(new { error = "granjaId es obligatorio." });
        if (request.KgTotal <= 0)
            return BadRequest(new { error = "El total de kg a aplicar debe ser mayor a 0." });

        try
        {
            var json = await _indicadorService.AplicarCorreccionSinPesoAsync(request, ct);
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al aplicar corrección de liquidación. granja={Granja} lote={Lote}",
                request.GranjaId, request.LoteCodigo);
            return StatusCode(500, new { error = "Error interno al aplicar la corrección.", message = ex.Message });
        }
    }
}
