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
    // LISTADO (opcional: filtrar por loteId + reproductoraId + rango de fechas)
    // ===========================
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LoteSeguimientoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LoteSeguimientoDto>>> GetAll(
        [FromQuery] string? loteId = null,
        [FromQuery] string? reproductoraId = null,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null)
    {
        if (!string.IsNullOrWhiteSpace(loteId) && !string.IsNullOrWhiteSpace(reproductoraId))
        {
            var items = await _svc.GetByLoteYReproAsync(loteId.Trim(), reproductoraId.Trim(), desde, hasta);
            return Ok(items);
        }
        var all = await _svc.GetAllAsync();
        return Ok(all);
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
        if (dto is null) return BadRequest(new { message = "El cuerpo de la petición es requerido." });

        try
        {
            var created = await _svc.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            var details = ex.InnerException?.Message ?? ex.Message;
            Console.WriteLine($"ERROR en Create LoteSeguimiento: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            return StatusCode(500, new {
                message = "Error al crear el seguimiento diario. Por favor, intente nuevamente.",
                details = details
            });
        }
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
        if (dto is null) return BadRequest(new { message = "El cuerpo de la petición es requerido." });
        if (dto.Id != id) return BadRequest(new { message = "El id de la ruta no coincide con el del cuerpo." });

        try
        {
            var updated = await _svc.UpdateAsync(dto);
            if (updated is null) return NotFound(new { message = "El seguimiento no fue encontrado." });
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            var details = ex.InnerException?.Message ?? ex.Message;
            Console.WriteLine($"ERROR en Update LoteSeguimiento: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            return StatusCode(500, new {
                message = "Error al actualizar el seguimiento diario. Por favor, intente nuevamente.",
                details = details
            });
        }
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
