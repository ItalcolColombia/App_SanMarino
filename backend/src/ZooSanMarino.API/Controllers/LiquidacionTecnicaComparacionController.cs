// backend/src/ZooSanMarino.API/Controllers/LiquidacionTecnicaComparacionController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LiquidacionTecnicaComparacionController : ControllerBase
{
    private readonly ILiquidacionTecnicaComparacionService _comparacionService;

    public LiquidacionTecnicaComparacionController(ILiquidacionTecnicaComparacionService comparacionService)
    {
        _comparacionService = comparacionService;
    }

    [HttpGet("test")]
    public ActionResult Test()
    {
        return Ok(new { message = "LiquidacionTecnicaComparacionController funcionando" });
    }

    /// <summary>
    /// Compara los datos del lote con la guía genética correspondiente
    /// </summary>
    /// <param name="loteId">ID del lote a comparar</param>
    /// <param name="fechaHasta">Fecha límite para el cálculo (opcional)</param>
    /// <returns>Comparación con la guía genética</returns>
    [HttpGet("lote/{loteId}")]
    [ProducesResponseType(typeof(LiquidacionTecnicaComparacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LiquidacionTecnicaComparacionDto>> CompararConGuiaGenetica(
        int loteId, 
        [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            var resultado = await _comparacionService.CompararConGuiaGeneticaAsync(loteId, fechaHasta);
            return Ok(resultado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene la comparación completa con detalles y seguimientos
    /// </summary>
    /// <param name="loteId">ID del lote a comparar</param>
    /// <param name="fechaHasta">Fecha límite para el cálculo (opcional)</param>
    /// <returns>Comparación completa con detalles</returns>
    [HttpGet("lote/{loteId}/completa")]
    [ProducesResponseType(typeof(LiquidacionTecnicaComparacionCompletaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LiquidacionTecnicaComparacionCompletaDto>> ObtenerComparacionCompleta(
        int loteId, 
        [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            var resultado = await _comparacionService.ObtenerComparacionCompletaAsync(loteId, fechaHasta);
            return Ok(resultado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
        }
    }

    /// <summary>
    /// Valida si un lote tiene guía genética configurada
    /// </summary>
    /// <param name="loteId">ID del lote a validar</param>
    /// <returns>Información sobre la configuración de guía genética</returns>
    [HttpGet("lote/{loteId}/validar-guia")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ValidarGuiaGeneticaConfigurada(int loteId)
    {
        try
        {
            var tieneGuia = await _comparacionService.ValidarGuiaGeneticaConfiguradaAsync(loteId);
            return Ok(new { tieneGuia });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene las líneas genéticas disponibles desde ProduccionAvicolaRaw
    /// </summary>
    /// <param name="raza">Filtrar por raza específica (opcional)</param>
    /// <param name="ano">Filtrar por año específico (opcional)</param>
    /// <returns>Lista de líneas genéticas disponibles</returns>
    [HttpGet("lineas-geneticas")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<object>>> ObtenerLineasGeneticasDisponibles(
        [FromQuery] string? raza = null,
        [FromQuery] int? ano = null)
    {
        try
        {
            var lineas = await _comparacionService.ObtenerLineasGeneticasDisponiblesAsync(raza, ano);
            return Ok(lineas);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
        }
    }
}