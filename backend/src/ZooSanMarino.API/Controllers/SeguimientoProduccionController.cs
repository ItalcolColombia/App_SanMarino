using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeguimientoProduccionController : ControllerBase
{
    private readonly ISeguimientoProduccionService _svc;

    public SeguimientoProduccionController(ISeguimientoProduccionService svc)
    {
        _svc = svc;
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



