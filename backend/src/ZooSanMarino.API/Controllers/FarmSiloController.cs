using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Silos y bodegas de una GRANJA: la ubicación real del inventario en empresas con
/// <c>ManejaInventarioPorSilo</c>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FarmSiloController : ControllerBase
{
    private readonly IFarmSiloService _svc;
    public FarmSiloController(IFarmSiloService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<FarmSiloDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FarmSiloDto>>> Get(
        [FromQuery] int? granjaId = null,
        [FromQuery] bool soloActivos = false,
        CancellationToken ct = default)
        => Ok(await _svc.GetAsync(granjaId, soloActivos, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FarmSiloDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmSiloDto>> GetById(int id, CancellationToken ct = default)
    {
        var dto = await _svc.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [ProducesResponseType(typeof(FarmSiloDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FarmSiloDto>> Create([FromBody] CreateFarmSiloDto dto, CancellationToken ct = default)
    {
        if (dto is null) return BadRequest(new { message = "El cuerpo de la petición es requerido." });
        try
        {
            var created = await _svc.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Fija de una vez qué silos del catálogo tiene la granja («le asigno a esa granja cuántos silos
    /// tiene»). Es un SET: lo que no venga se da de baja si no está en uso.
    /// </summary>
    [HttpPost("asignar-desde-catalogo")]
    [ProducesResponseType(typeof(IEnumerable<FarmSiloDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<FarmSiloDto>>> AsignarDesdeCatalogo([FromBody] AsignarSilosGranjaDto dto, CancellationToken ct = default)
    {
        if (dto is null) return BadRequest(new { message = "El cuerpo de la petición es requerido." });
        try { return Ok(await _svc.AsignarDesdeCatalogoAsync(dto, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(FarmSiloDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmSiloDto>> Update(int id, [FromBody] UpdateFarmSiloDto dto, CancellationToken ct = default)
    {
        if (dto is null) return BadRequest(new { message = "El cuerpo de la petición es requerido." });
        try
        {
            var updated = await _svc.UpdateAsync(id, dto, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        try { return await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
