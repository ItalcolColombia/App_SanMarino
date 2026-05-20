using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class SeguimientoAvesEngordeEcuadorController : ControllerBase
{
    private readonly ISeguimientoAvesEngordeEcuadorService _svc;

    public SeguimientoAvesEngordeEcuadorController(ISeguimientoAvesEngordeEcuadorService svc)
    {
        _svc = svc;
    }

    /// <summary>Obtener un registro por ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SeguimientoLoteLevanteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeguimientoLoteLevanteDto>> GetById(int id)
    {
        var item = await _svc.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Registros diarios del lote + historial unificado (inventario y ventas), orden cronológico.</summary>
    [HttpGet("por-lote/{loteId:int}")]
    [ProducesResponseType(typeof(SeguimientoAvesEngordePorLoteResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SeguimientoAvesEngordePorLoteResponseDto>> GetByLote(int loteId)
    {
        var result = await _svc.GetByLoteAsync(loteId);
        return Ok(result);
    }

    /// <summary>Tabla diaria calculada vía función SQL (fn_seguimiento_diario_engorde) para el lote indicado.</summary>
    [HttpGet("por-lote/{loteId:int}/tabla-diaria")]
    [ProducesResponseType(typeof(IReadOnlyList<SeguimientoDiarioTablaFilaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SeguimientoDiarioTablaFilaDto>>> GetTablaDiaria([FromRoute] int loteId)
    {
        try
        {
            var result = await _svc.GetTablaDiariaAsync(loteId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Filtrar registros por lote y rango de fechas.</summary>
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

    /// <summary>Crear un nuevo registro diario (Ecuador).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SeguimientoLoteLevanteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeguimientoLoteLevanteDto>> Create([FromBody] CreateSeguimientoLoteLevanteRequest request)
    {
        if (request is null) return BadRequest(new { message = "Body requerido." });
        try
        {
            var dto = request.ToDto();
            var result = await _svc.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            return BadRequest(new { message = "Ya existe un registro de seguimiento diario para este lote en la fecha seleccionada. Solo puede haber un registro por lote por día.", detail = pg.Message });
        }
    }

    /// <summary>Editar un registro diario (Ecuador).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SeguimientoLoteLevanteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeguimientoLoteLevanteDto>> Update(int id, [FromBody] CreateSeguimientoLoteLevanteRequest request)
    {
        if (request is null) return BadRequest("Body requerido.");
        try
        {
            var dto = request.ToDto(id);
            var updated = await _svc.UpdateAsync(dto);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            return BadRequest(new { message = "Ya existe un registro de seguimiento diario para este lote en la fecha seleccionada.", detail = pg.Message });
        }
    }

    /// <summary>Eliminar un registro diario (Ecuador).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _svc.DeleteAsync(id);
            return deleted ? NoContent() : NotFound(new { message = "Registro no encontrado." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
