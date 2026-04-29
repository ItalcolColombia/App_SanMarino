using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LiquidacionCierreLoteLevanteController : ControllerBase
{
    private readonly ILiquidacionCierreLoteLevanteService _svc;
    private readonly ILogger<LiquidacionCierreLoteLevanteController> _logger;

    public LiquidacionCierreLoteLevanteController(
        ILiquidacionCierreLoteLevanteService svc,
        ILogger<LiquidacionCierreLoteLevanteController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    /// <summary>
    /// Calcula las variables de liquidación técnica a semana 25 sin guardar.
    /// Usa SeguimientoDiario y la guía genética del lote.
    /// </summary>
    [HttpGet("{lotePosturaLevanteId:int}/calcular")]
    public async Task<ActionResult<LiquidacionCierreLoteLevanteDto>> Calcular(
        int lotePosturaLevanteId,
        CancellationToken ct = default)
    {
        try
        {
            var resultado = await _svc.CalcularAsync(lotePosturaLevanteId, ct);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Error calculando liquidación cierre lote {Id}: {Msg}", lotePosturaLevanteId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interno calculando liquidación cierre lote {Id}", lotePosturaLevanteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Guarda (o actualiza) la liquidación de cierre del lote en la base de datos.
    /// Se llama al confirmar el cierre del lote de levante.
    /// </summary>
    [HttpPost("{lotePosturaLevanteId:int}/guardar")]
    public async Task<ActionResult<LiquidacionCierreGuardadaDto>> Guardar(
        int lotePosturaLevanteId,
        CancellationToken ct = default)
    {
        try
        {
            var resultado = await _svc.GuardarAsync(lotePosturaLevanteId, ct);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Error guardando liquidación cierre lote {Id}: {Msg}", lotePosturaLevanteId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interno guardando liquidación cierre lote {Id}", lotePosturaLevanteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene la liquidación de cierre ya guardada para un lote.
    /// </summary>
    [HttpGet("{lotePosturaLevanteId:int}")]
    public async Task<ActionResult<LiquidacionCierreGuardadaDto>> ObtenerPorLote(
        int lotePosturaLevanteId,
        CancellationToken ct = default)
    {
        try
        {
            var resultado = await _svc.ObtenerPorLoteAsync(lotePosturaLevanteId, ct);
            if (resultado is null) return NotFound(new { error = "No hay liquidación guardada para este lote." });
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo liquidación cierre lote {Id}", lotePosturaLevanteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }
}
