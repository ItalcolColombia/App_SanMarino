// src/ZooSanMarino.API/Controllers/ProduccionController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProduccionController : ControllerBase
{
    private readonly IProduccionService _produccionService;

    public ProduccionController(IProduccionService produccionService)
    {
        _produccionService = produccionService;
    }

    /// <summary>
    /// Verifica si existe un registro inicial de producción para un lote
    /// </summary>
    /// <param name="loteId">ID del lote</param>
    /// <returns>Información sobre la existencia del registro inicial</returns>
    [HttpGet("lotes/{loteId}/exists")]
    public async Task<ActionResult<ExisteProduccionLoteResponse>> ExisteProduccionLote(int loteId)
    {
        try
        {
            var existe = await _produccionService.ExisteProduccionLoteAsync(loteId);
            var produccionLoteId = existe ? await _produccionService.ObtenerProduccionLoteAsync(loteId) : null;
            
            return Ok(new ExisteProduccionLoteResponse(existe, produccionLoteId?.Id));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Crea un nuevo registro inicial de producción para un lote
    /// </summary>
    /// <param name="request">Datos del registro inicial</param>
    /// <returns>ID del registro creado</returns>
    [HttpPost("lotes")]
    public async Task<ActionResult<int>> CrearProduccionLote([FromBody] CrearProduccionLoteRequest request)
    {
        try
        {
            var id = await _produccionService.CrearProduccionLoteAsync(request);
            return CreatedAtAction(nameof(ObtenerProduccionLote), new { loteId = request.LoteId }, id);
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
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el detalle del registro inicial de producción de un lote
    /// </summary>
    /// <param name="loteId">ID del lote</param>
    /// <returns>Detalle del registro inicial</returns>
    [HttpGet("lotes/{loteId}")]
    public async Task<ActionResult<ProduccionLoteDetalleDto>> ObtenerProduccionLote(int loteId)
    {
        try
        {
            var detalle = await _produccionService.ObtenerProduccionLoteAsync(loteId);
            
            if (detalle == null)
            {
                return NotFound(new { message = "No se encontró un registro inicial de producción para este lote" });
            }

            return Ok(detalle);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Crea un nuevo seguimiento diario de producción
    /// </summary>
    /// <param name="request">Datos del seguimiento diario</param>
    /// <returns>ID del seguimiento creado</returns>
    [HttpPost("seguimiento")]
    public async Task<ActionResult<int>> CrearSeguimiento([FromBody] CrearSeguimientoRequest request)
    {
        try
        {
            var id = await _produccionService.CrearSeguimientoAsync(request);
            return CreatedAtAction(nameof(ListarSeguimiento), new { loteId = request.ProduccionLoteId }, id);
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
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Lista los seguimientos diarios de producción de un lote
    /// </summary>
    /// <param name="loteId">ID del lote</param>
    /// <param name="desde">Fecha desde (opcional)</param>
    /// <param name="hasta">Fecha hasta (opcional)</param>
    /// <param name="page">Número de página (por defecto 1)</param>
    /// <param name="size">Tamaño de página (por defecto 10)</param>
    /// <returns>Lista paginada de seguimientos</returns>
    [HttpGet("seguimiento")]
    public async Task<ActionResult<ListaSeguimientoResponse>> ListarSeguimiento(
        [FromQuery] int loteId,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        try
        {
            if (page < 1) page = 1;
            if (size < 1 || size > 100) size = 10;

            var resultado = await _produccionService.ListarSeguimientoAsync(loteId, desde, hasta, page, size);
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}
