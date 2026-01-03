// file: backend/src/ZooSanMarino.API/Controllers/SeguimientoLoteLevanteController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeguimientoLoteLevanteController : ControllerBase
{
    private readonly ISeguimientoLoteLevanteService _svc;
    public SeguimientoLoteLevanteController(ISeguimientoLoteLevanteService svc) => _svc = svc;

    /// <summary>Obtener todos los registros de un lote (ordenados por fecha asc).</summary>
    [HttpGet("por-lote/{loteId}")]
    [ProducesResponseType(typeof(IEnumerable<SeguimientoLoteLevanteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeguimientoLoteLevanteDto>>> GetByLote(int loteId)
    {
        var items = await _svc.GetByLoteAsync(loteId);
        return Ok(items);
    }

    /// <summary>Crear un nuevo registro diario. Acepta consumo en kg o gramos, el backend hace la conversión.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SeguimientoLoteLevanteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeguimientoLoteLevanteDto>> Create([FromBody] CreateSeguimientoLoteLevanteRequest request)
    {
        if (request is null) return BadRequest("Body requerido.");
        try
        {
            // El request convierte automáticamente las unidades a kg
            var dto = request.ToDto();
            var result = await _svc.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByLote), new { loteId = result.LoteId }, result);
        }
        catch (InvalidOperationException ex)
        {
            // Errores de negocio del servicio (lote inexistente, duplicado por fecha, etc.)
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Editar un registro diario. Acepta consumo en kg o gramos, el backend hace la conversión.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SeguimientoLoteLevanteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeguimientoLoteLevanteDto>> Update(int id, [FromBody] CreateSeguimientoLoteLevanteRequest request)
    {
        if (request is null) return BadRequest("Body requerido.");
        
        try
        {
            // El request convierte automáticamente las unidades a kg
            var dto = request.ToDto(id);
            var updated = await _svc.UpdateAsync(dto);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Eliminar un registro diario.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _svc.DeleteAsync(id);
            if (!deleted)
            {
                // Podría ser que no existe o que no pertenece a la compañía del usuario
                return NotFound(new { message = "Registro no encontrado o no tienes permisos para eliminarlo." });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error al eliminar el registro.", error = ex.Message });
        }
    }

    /// <summary>Filtrar por fechas opcionalmente con loteId.</summary>
    [HttpGet("filtro")]
    [ProducesResponseType(typeof(IEnumerable<SeguimientoLoteLevanteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeguimientoLoteLevanteDto>>> Filter(
        [FromQuery] int? loteId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var result = await _svc.FilterAsync(loteId, desde, hasta);
        return Ok(result);
    }

    /// <summary>Resultado calculado (ejecuta SP y devuelve snapshot del lote).</summary>
    [HttpGet("por-lote/{loteId}/resultado")]
    [ProducesResponseType(typeof(ResultadoLevanteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoLevanteResponse>> GetResultadoPorLote(
        int loteId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] bool recalcular = true)
    {
        try
        {
            var res = await _svc.GetResultadoAsync(loteId, desde, hasta, recalcular);
            return Ok(res);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
