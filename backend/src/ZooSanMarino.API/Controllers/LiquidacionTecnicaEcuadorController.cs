// API de Liquidación Técnica para Ecuador: lote aves de engorde (LoteAveEngordeId).
// Rutas compatibles con el frontend que usa LiquidacionTecnica + LiquidacionTecnicaComparacion.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LiquidacionTecnicaEcuadorController : ControllerBase
{
    private readonly ILiquidacionTecnicaEcuadorService _service;
    private readonly ILogger<LiquidacionTecnicaEcuadorController> _logger;

    public LiquidacionTecnicaEcuadorController(
        ILiquidacionTecnicaEcuadorService service,
        ILogger<LiquidacionTecnicaEcuadorController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Calcula la liquidación técnica de un lote de aves de engorde (Ecuador).</summary>
    [HttpGet("{loteAveEngordeId:int}")]
    [ProducesResponseType(typeof(LiquidacionTecnicaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LiquidacionTecnicaDto>> CalcularLiquidacion(
        int loteAveEngordeId,
        [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            var resultado = await _service.CalcularLiquidacionAsync(loteAveEngordeId, fechaHasta);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Liquidación Ecuador lote {Id}: {Error}", loteAveEngordeId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Obtiene la liquidación técnica completa con detalles (Ecuador).</summary>
    [HttpGet("{loteAveEngordeId:int}/completa")]
    [ProducesResponseType(typeof(LiquidacionTecnicaCompletaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LiquidacionTecnicaCompletaDto>> ObtenerLiquidacionCompleta(
        int loteAveEngordeId,
        [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            var resultado = await _service.ObtenerLiquidacionCompletaAsync(loteAveEngordeId, fechaHasta);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Liquidación completa Ecuador lote {Id}: {Error}", loteAveEngordeId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Compara con guía genética (Ecuador).</summary>
    [HttpGet("lote/{loteAveEngordeId:int}")]
    [ProducesResponseType(typeof(LiquidacionTecnicaComparacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LiquidacionTecnicaComparacionDto>> CompararConGuiaGenetica(
        int loteAveEngordeId,
        [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            var resultado = await _service.CompararConGuiaGeneticaAsync(loteAveEngordeId, fechaHasta);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Comparación Ecuador lote {Id}: {Error}", loteAveEngordeId, ex.Message);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Obtiene la comparación completa con detalles (Ecuador).</summary>
    [HttpGet("lote/{loteAveEngordeId:int}/completa")]
    [ProducesResponseType(typeof(LiquidacionTecnicaComparacionCompletaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LiquidacionTecnicaComparacionCompletaDto>> ObtenerComparacionCompleta(
        int loteAveEngordeId,
        [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            var resultado = await _service.ObtenerComparacionCompletaAsync(loteAveEngordeId, fechaHasta);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Comparación completa Ecuador lote {Id}: {Error}", loteAveEngordeId, ex.Message);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Valida si el lote de aves de engorde puede calcular liquidación.</summary>
    [HttpGet("{loteAveEngordeId:int}/validar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> ValidarLote(int loteAveEngordeId)
    {
        try
        {
            var esValido = await _service.ValidarLoteParaLiquidacionAsync(loteAveEngordeId);
            return Ok(new { loteAveEngordeId, esValido, mensaje = esValido ? "Lote válido para liquidación" : "Lote no válido o sin datos de seguimiento" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validar lote Ecuador {Id}", loteAveEngordeId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }
}
