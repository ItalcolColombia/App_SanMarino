using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Lista MAESTRA de silos de la empresa activa (1..100). De acá salen los silos que después se
/// asignan a cada granja. Solo tiene sentido en empresas con <c>ManejaInventarioPorSilo</c>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SiloCatalogoController : ControllerBase
{
    private readonly ISiloCatalogoService _svc;
    public SiloCatalogoController(ISiloCatalogoService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SiloCatalogoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SiloCatalogoDto>>> GetAll([FromQuery] bool soloActivos = false, CancellationToken ct = default)
        => Ok(await _svc.GetAllAsync(soloActivos, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SiloCatalogoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SiloCatalogoDto>> GetById(int id, CancellationToken ct = default)
    {
        var dto = await _svc.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SiloCatalogoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SiloCatalogoDto>> Create([FromBody] CreateSiloCatalogoDto dto, CancellationToken ct = default)
    {
        if (dto is null) return BadRequest(new { message = "El cuerpo de la petición es requerido." });
        try
        {
            var created = await _svc.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Genera de una sola vez el rango completo (el «lista del 1 al 100»). Idempotente.</summary>
    [HttpPost("generar-rango")]
    [ProducesResponseType(typeof(GenerarRangoSilosResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GenerarRangoSilosResultDto>> GenerarRango([FromBody] GenerarRangoSilosDto dto, CancellationToken ct = default)
    {
        if (dto is null) return BadRequest(new { message = "El cuerpo de la petición es requerido." });
        try { return Ok(await _svc.GenerarRangoAsync(dto, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SiloCatalogoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SiloCatalogoDto>> Update(int id, [FromBody] UpdateSiloCatalogoDto dto, CancellationToken ct = default)
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
