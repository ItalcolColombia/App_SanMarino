// API para Seguimiento Diario Aves de Engorde (seguimiento_diario tipo = 'engorde'). Misma forma que SeguimientoLoteLevante.
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SeguimientoAvesEngordeController : ControllerBase
{
    private readonly ISeguimientoAvesEngordeService _svc;
    private readonly IServiceScopeFactory _scopeFactory;

    public SeguimientoAvesEngordeController(ISeguimientoAvesEngordeService svc, IServiceScopeFactory scopeFactory)
    {
        _svc = svc;
        _scopeFactory = scopeFactory;
    }

    /// <summary>Datos para filtros en cascada (Granja → Núcleo → Galpón → Lote). Lotes = lote_ave_engorde (no lotes levante).</summary>
    [HttpGet("filter-data")]
    [ProducesResponseType(typeof(LoteReproductoraFilterDataDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoteReproductoraFilterDataDto>> GetFilterData(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var filterDataSvc = scope.ServiceProvider.GetRequiredService<ISeguimientoAvesEngordeFilterDataService>();
        var data = await filterDataSvc.GetFilterDataAsync(ct);
        return Ok(data);
    }

    /// <summary>Obtener todos los registros de un lote (ordenados por fecha asc).</summary>
    [HttpGet("por-lote/{loteId}")]
    [ProducesResponseType(typeof(IEnumerable<SeguimientoLoteLevanteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeguimientoLoteLevanteDto>>> GetByLote(int loteId)
    {
        var items = await _svc.GetByLoteAsync(loteId);
        return Ok(items);
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

    /// <summary>Crear un nuevo registro diario.</summary>
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
            return CreatedAtAction(nameof(GetByLote), new { loteId = result.LoteId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            var pgEx = (PostgresException)ex.InnerException;
            if (string.Equals(pgEx.ConstraintName, "uq_seg_diario_aves_engorde_lote_fecha", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Ya existe un registro de seguimiento diario para este lote en la fecha seleccionada. Solo puede haber un registro por lote por día." });
            return BadRequest(new { message = "Error de duplicado en la base de datos.", detail = pgEx.Message });
        }
    }

    /// <summary>Editar un registro diario.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SeguimientoLoteLevanteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
            var pgEx = (PostgresException)ex.InnerException;
            if (string.Equals(pgEx.ConstraintName, "uq_seg_diario_aves_engorde_lote_fecha", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Ya existe un registro de seguimiento diario para este lote en la fecha seleccionada. Solo puede haber un registro por lote por día." });
            return BadRequest(new { message = "Error de duplicado en la base de datos.", detail = pgEx.Message });
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
                return NotFound(new { message = "Registro no encontrado o no tienes permisos para eliminarlo." });
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

    /// <summary>Resultado calculado (para Aves de Engorde devuelve lista vacía; estructura compatible con Levante).</summary>
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
