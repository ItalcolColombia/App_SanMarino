// API para Seguimiento Diario Lote Reproductora Aves de Engorde (tabla seguimiento_diario_lote_reproductora_aves_engorde).
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SeguimientoDiarioLoteReproductoraController : ControllerBase
{
    private readonly ISeguimientoDiarioLoteReproductoraService _svc;
    private readonly IServiceScopeFactory _scopeFactory;

    public SeguimientoDiarioLoteReproductoraController(
        ISeguimientoDiarioLoteReproductoraService svc,
        IServiceScopeFactory scopeFactory)
    {
        _svc = svc;
        _scopeFactory = scopeFactory;
    }

    /// <summary>Datos para filtros en cascada (Granja → Núcleo → Galpón → Lote Aves Engorde → Lote Reproductora).</summary>
    [HttpGet("filter-data")]
    [ProducesResponseType(typeof(SeguimientoDiarioLoteReproductoraFilterDataDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SeguimientoDiarioLoteReproductoraFilterDataDto>> GetFilterData(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var filterDataSvc = scope.ServiceProvider.GetRequiredService<ISeguimientoDiarioLoteReproductoraFilterDataService>();
        var data = await filterDataSvc.GetFilterDataAsync(ct);
        return Ok(data);
    }

    /// <summary>Obtener todos los registros de un lote reproductora (ordenados por fecha asc).</summary>
    [HttpGet("por-lote-reproductora/{loteReproductoraId:int}")]
    [ProducesResponseType(typeof(IEnumerable<SeguimientoLoteLevanteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeguimientoLoteLevanteDto>>> GetByLoteReproductora(int loteReproductoraId)
    {
        var items = await _svc.GetByLoteReproductoraAsync(loteReproductoraId);
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

    /// <summary>Crear un nuevo registro diario. LoteId = lote_reproductora_ave_engorde_id. Request propio (CreateSeguimientoDiarioLoteReproductoraRequest). Persiste en seguimiento_diario_lote_reproductora_aves_engorde.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SeguimientoLoteLevanteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeguimientoLoteLevanteDto>> Create([FromBody] CreateSeguimientoDiarioLoteReproductoraRequest request)
    {
        if (request is null) return BadRequest("Body requerido.");
        try
        {
            var dto = request.ToDto();
            var result = await _svc.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByLoteReproductora), new { loteReproductoraId = result.LoteId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Editar un registro diario (tabla seguimiento_diario_lote_reproductora_aves_engorde). Request propio CreateSeguimientoDiarioLoteReproductoraRequest.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SeguimientoLoteLevanteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeguimientoLoteLevanteDto>> Update(int id, [FromBody] CreateSeguimientoDiarioLoteReproductoraRequest request)
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
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Eliminar un registro diario.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _svc.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { message = "Registro no encontrado o no tienes permisos para eliminarlo." });
        return NoContent();
    }

    /// <summary>Filtrar por fechas opcionalmente con loteReproductoraId.</summary>
    [HttpGet("filtro")]
    [ProducesResponseType(typeof(IEnumerable<SeguimientoLoteLevanteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeguimientoLoteLevanteDto>>> Filter(
        [FromQuery] int? loteReproductoraId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var result = await _svc.FilterAsync(loteReproductoraId, desde, hasta);
        return Ok(result);
    }
}
