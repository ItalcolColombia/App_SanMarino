// file: src/ZooSanMarino.API/Controllers/LoteSeguimientoController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoteSeguimientoController : ControllerBase
{
    private readonly ILoteSeguimientoService _svc;
    public LoteSeguimientoController(ILoteSeguimientoService svc) => _svc = svc;

    // ===========================
    // LISTADO
    // ===========================
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LoteSeguimientoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LoteSeguimientoDto>>> GetAll()
    {
        var items = await _svc.GetAllAsync();
        return Ok(items);
    }

    // ===========================
    // DETALLE POR ID
    // ===========================
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LoteSeguimientoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteSeguimientoDto>> GetById(int id)
    {
        var dto = await _svc.GetByIdAsync(id);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    // ===========================
    // CREAR
    // ===========================
    [HttpPost]
    [ProducesResponseType(typeof(LoteSeguimientoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoteSeguimientoDto>> Create([FromBody] CreateLoteSeguimientoDto dto)
    {
        if (dto is null) return BadRequest("Body requerido.");

        var created = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // ===========================
    // ACTUALIZAR
    // ===========================
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(LoteSeguimientoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteSeguimientoDto>> Update(int id, [FromBody] UpdateLoteSeguimientoDto dto)
    {
        if (dto is null) return BadRequest("Body requerido.");
        if (dto.Id != id) return BadRequest("El id de la ruta no coincide con el del cuerpo.");

        var updated = await _svc.UpdateAsync(dto);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    // ===========================
    // ELIMINAR
    // ===========================
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _svc.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }
}
