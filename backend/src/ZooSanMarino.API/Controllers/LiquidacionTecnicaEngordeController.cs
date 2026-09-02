// API de Liquidación Técnica de POLLO ENGORDE: opera sobre `LoteAveEngordeId`.
// Su hermano `LiquidacionTecnicaController` es el de LEVANTE — esa es la diferencia real entre los
// dos, no el país: hasta sep-2026 este se llamaba `LiquidacionTecnicaEcuador` y el nombre hacía
// creer que había un camino por país. No lo hay.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
// Ruta neutra. Va en PascalCase, igual que su hermano de levante (`api/LiquidacionTecnica`), para
// no partir la simetría del par.
[Route("api/LiquidacionTecnicaEngorde")]
// Ruta histórica, conservada como ALIAS: la piden el front ya desplegado y cualquier cliente que
// todavía no bajó el bundle nuevo. La ruta salía de `[controller]`, así que sin este alias el
// rename de la clase la habría cambiado sola.
[Route("api/LiquidacionTecnicaEcuador")]
// Fija el grupo de Swagger: con dos `[Route]` cada acción aparece una vez por ruta, y sin `[Tags]`
// el nombre del grupo lo decide el nombre de la clase. Mismo patrón que `GuiaGeneticaEngordeController`.
[Tags("LiquidacionTecnicaEngorde")]
[Authorize]
public class LiquidacionTecnicaEngordeController : ControllerBase
{
    private readonly ILiquidacionTecnicaEngordeService _service;
    private readonly ILogger<LiquidacionTecnicaEngordeController> _logger;

    public LiquidacionTecnicaEngordeController(
        ILiquidacionTecnicaEngordeService service,
        ILogger<LiquidacionTecnicaEngordeController> logger)
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
