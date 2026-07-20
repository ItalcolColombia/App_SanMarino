// src/ZooSanMarino.API/Controllers/ReporteIndicadorPanamaController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReporteIndicadorPanamaController : ControllerBase
{
    private readonly IReporteIndicadorPanamaService _service;
    private readonly ILogger<ReporteIndicadorPanamaController> _logger;

    public ReporteIndicadorPanamaController(
        IReporteIndicadorPanamaService service,
        ILogger<ReporteIndicadorPanamaController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Guarda/actualiza los 6 insumos de liquidación Panamá de un lote (días en granja, días de
    /// engorde, aves finales, aves beneficiadas, producción kilo en pie, metros cuadrados).
    /// </summary>
    [HttpPost("liquidar")]
    public async Task<ActionResult> Liquidar([FromBody] GuardarLiquidacionPanamaRequest request, CancellationToken ct)
    {
        if (request is null || request.LoteAveEngordeId <= 0)
            return BadRequest(new { error = "LoteAveEngordeId es requerido y debe ser mayor a 0." });
        try
        {
            var id = await _service.GuardarLiquidacionAsync(request, ct);
            return Ok(new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar liquidación Panamá. Request: {@Request}", request);
            return StatusCode(500, new { error = "Error interno al guardar la liquidación." });
        }
    }

    /// <summary>
    /// Reporte de una CORRIDA completa (Panamá): lote_nombre = número de corrida, repetido en
    /// varios galpones de la granja. Devuelve un reporte por galpón + consolidado de la corrida
    /// y lista los galpones que aún no tienen liquidación registrada.
    /// 404 si la corrida no tiene lotes en la granja (empresa activa).
    /// </summary>
    [HttpGet("por-corrida")]
    public async Task<ActionResult<ReporteCorridaPanamaDto>> GetReportePorCorrida(
        [FromQuery] int granjaId,
        [FromQuery] string corrida,
        [FromQuery] string? nucleoId,
        [FromQuery] string? galponId,
        CancellationToken ct)
    {
        if (granjaId <= 0 || string.IsNullOrWhiteSpace(corrida))
            return BadRequest(new { error = "granjaId y corrida son requeridos." });
        try
        {
            var reporte = await _service.GetReportePorCorridaAsync(granjaId, corrida, nucleoId, galponId, ct);
            if (reporte is null)
                return NotFound(new { error = $"La corrida '{corrida.Trim()}' no tiene lotes en la granja indicada." });
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte por corrida Panamá. Granja: {GranjaId}, Corrida: {Corrida}", granjaId, corrida);
            return StatusCode(500, new { error = "Error interno al generar el reporte de la corrida." });
        }
    }

    /// <summary>
    /// Reporte "RESULTADOS DE LIQUIDACIÓN" del lote (ejecuta fn_reporte_indicadores_panama).
    /// 404 si el lote aún no tiene liquidación registrada.
    /// </summary>
    [HttpGet("{loteAveEngordeId:int}")]
    public async Task<ActionResult<ReporteIndicadoresPanamaDto>> GetReporte(int loteAveEngordeId, CancellationToken ct)
    {
        try
        {
            var reporte = await _service.GetReporteAsync(loteAveEngordeId, ct);
            if (reporte is null)
                return NotFound(new { error = "El lote no tiene liquidación Panamá registrada." });
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte indicadores Panamá. Lote: {LoteId}", loteAveEngordeId);
            return StatusCode(500, new { error = "Error interno al generar el reporte." });
        }
    }
}
