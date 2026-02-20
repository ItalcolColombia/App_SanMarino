using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SeguimientoProduccionController : ControllerBase
{
    private readonly ISeguimientoProduccionService _svc;
    private readonly IServiceScopeFactory _scopeFactory;

    public SeguimientoProduccionController(ISeguimientoProduccionService svc, IServiceScopeFactory scopeFactory)
    {
        _svc = svc;
        _scopeFactory = scopeFactory;
    }

    /// <summary>Datos para filtros en cascada (Granja → Núcleo → Galpón → Lote) con solo lotes de producción.</summary>
    [HttpGet("filter-data")]
    [ProducesResponseType(typeof(LoteReproductoraFilterDataDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoteReproductoraFilterDataDto>> GetFilterData(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var filterDataSvc = scope.ServiceProvider.GetRequiredService<ILoteProduccionFilterDataService>();
        var data = await filterDataSvc.GetFilterDataAsync(ct);
        return Ok(data);
    }

    // ✅ Crear nuevo registro de seguimiento
    [HttpPost]
    public async Task<IActionResult> Create(CreateSeguimientoProduccionDto dto)
    {
        var result = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetByLoteId), new { loteId = result.LoteId }, result);
    }

    // ✅ Obtener todos los registros
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _svc.GetAllAsync();
        return Ok(result);
    }

    // ✅ Obtener por LoteId
    [HttpGet("{loteId}")]
    public async Task<IActionResult> GetByLoteId(int loteId)
    {
        var result = await _svc.GetByLoteIdAsync(loteId);
        return result is null ? NotFound() : Ok(result);
    }

    // ✅ Actualizar registro
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateSeguimientoProduccionDto dto)
    {
        if (id != dto.Id) return BadRequest("El ID de la ruta no coincide con el del cuerpo.");
        
        var updated = await _svc.UpdateAsync(dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    // ✅ Eliminar registro
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _svc.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    // ✅ Filtro por lote y/o fechas
    [HttpGet("filter")]
    public async Task<IActionResult> Filter([FromQuery] int? loteId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var filter = new FilterSeguimientoProduccionDto(loteId, desde, hasta);
        var result = await _svc.FilterAsync(filter);
        return Ok(result);
    }
}



