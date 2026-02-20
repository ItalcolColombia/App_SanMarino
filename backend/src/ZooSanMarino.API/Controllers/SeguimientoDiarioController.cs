// file: src/ZooSanMarino.API/Controllers/SeguimientoDiarioController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// API CRUD y filtrado para la tabla unificada de seguimiento diario (levante, producción, reproductora).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SeguimientoDiarioController : ControllerBase
{
    private readonly ISeguimientoDiarioService _svc;

    public SeguimientoDiarioController(ISeguimientoDiarioService svc)
    {
        _svc = svc;
    }

    /// <summary>
    /// Obtiene un registro por ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(SeguimientoDiarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeguimientoDiarioDto>> GetById(long id, CancellationToken ct)
    {
        var dto = await _svc.GetByIdAsync(id, ct);
        if (dto is null)
            return NotFound(new { message = "Registro no encontrado." });
        return Ok(dto);
    }

    /// <summary>
    /// Lista registros con filtros y paginación.
    /// Query: tipoSeguimiento, loteId, reproductoraId, fechaDesde, fechaHasta, page, pageSize, orderBy, orderAsc
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ZooSanMarino.Application.DTOs.Common.PagedResult<SeguimientoDiarioDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ZooSanMarino.Application.DTOs.Common.PagedResult<SeguimientoDiarioDto>>> GetFiltered(
        [FromQuery] string? tipoSeguimiento = null,
        [FromQuery] string? loteId = null,
        [FromQuery] string? reproductoraId = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string orderBy = "Fecha",
        [FromQuery] bool orderAsc = false,
        CancellationToken ct = default)
    {
        var filter = new SeguimientoDiarioFilterRequest
        {
            TipoSeguimiento = tipoSeguimiento,
            LoteId = loteId,
            ReproductoraId = reproductoraId,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            Page = page,
            PageSize = Math.Clamp(pageSize, 1, 100),
            OrderBy = orderBy,
            OrderAsc = orderAsc
        };
        var result = await _svc.GetFilteredAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Crea un nuevo registro de seguimiento diario.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SeguimientoDiarioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeguimientoDiarioDto>> Create(
        [FromBody] CreateSeguimientoDiarioDto dto,
        CancellationToken ct)
    {
        if (dto is null)
            return BadRequest(new { message = "El cuerpo de la petición es requerido." });

        try
        {
            var created = await _svc.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            var details = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Error al crear el seguimiento diario.", details });
        }
    }

    /// <summary>
    /// Actualiza un registro existente.
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(SeguimientoDiarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeguimientoDiarioDto>> Update(
        long id,
        [FromBody] UpdateSeguimientoDiarioDto dto,
        CancellationToken ct)
    {
        if (dto is null)
            return BadRequest(new { message = "El cuerpo de la petición es requerido." });
        if (dto.Id != id)
            return BadRequest(new { message = "El id de la ruta no coincide con el del cuerpo." });

        try
        {
            var updated = await _svc.UpdateAsync(dto, ct);
            if (updated is null)
                return NotFound(new { message = "Registro no encontrado." });
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            var details = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Error al actualizar el seguimiento diario.", details });
        }
    }

    /// <summary>
    /// Elimina un registro.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var ok = await _svc.DeleteAsync(id, ct);
        return ok ? NoContent() : NotFound(new { message = "Registro no encontrado." });
    }
}
