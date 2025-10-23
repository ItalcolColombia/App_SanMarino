using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Application.DTOs;                     // CreateLoteDto, UpdateLoteDto, LoteDto
using CommonDtos = ZooSanMarino.Application.DTOs.Common; // PagedResult<T>
using LoteDtos   = ZooSanMarino.Application.DTOs.Lotes;
using ZooSanMarino.Application.DTOs.Lotes;  // LoteDetailDto, LoteSearchRequest

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Consumes("application/json")]
public class LoteController : ControllerBase
{
    private readonly ILoteService _svc;
    public LoteController(ILoteService svc) => _svc = svc;

    // ===========================
    // LISTADO SIMPLE CON INFORMACIÓN COMPLETA DE RELACIONES
    // ===========================
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LoteDtos.LoteDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LoteDtos.LoteDetailDto>>> GetAll()
    {
        var items = await _svc.GetAllAsync();
        return Ok(items);
    }

    // ===========================
    // BÚSQUEDA AVANZADA
    // ===========================
    [HttpGet("search")]
    [ProducesResponseType(typeof(CommonDtos.PagedResult<LoteDtos.LoteDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CommonDtos.PagedResult<LoteDtos.LoteDetailDto>>> Search([FromQuery] LoteDtos.LoteSearchRequest req)
    {
        var res = await _svc.SearchAsync(req);
        return Ok(res);
    }

    // ===========================
    // DETALLE
    // ===========================
    [HttpGet("{loteId}")]
    [ProducesResponseType(typeof(LoteDtos.LoteDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteDtos.LoteDetailDto>> GetById(int loteId)
    {
        var res = await _svc.GetByIdAsync(loteId);
        if (res is null) return NotFound();
        return Ok(res);
    }

    // ===========================
    // CREATE
    // ===========================
    [HttpPost]
    [ProducesResponseType(typeof(LoteDtos.LoteDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoteDtos.LoteDetailDto>> Create([FromBody] CreateLoteDto dto)
    {
        if (dto is null) return BadRequest("Body requerido.");
        // LoteId es opcional - el backend generará automáticamente si no se proporciona

        try
        {
            var created = await _svc.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { loteId = created.LoteId }, created);
        }
        catch (InvalidOperationException ex)
        {
            // Reglas de negocio (pertenencia, duplicados, etc.)
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ===========================
    // UPDATE
    // ===========================
    [HttpPut("{loteId}")]
    [ProducesResponseType(typeof(LoteDtos.LoteDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteDtos.LoteDetailDto>> Update(int loteId, [FromBody] UpdateLoteDto dto)
    {
        if (dto is null) return BadRequest("Body requerido.");
        if (dto.LoteId <= 0)
            return BadRequest("LoteId debe ser mayor que 0.");
        if (loteId != dto.LoteId)
            return BadRequest("El id de la ruta no coincide con el del cuerpo.");

        try
        {
            var updated = await _svc.UpdateAsync(dto);
            if (updated is null) return NotFound();
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ===========================
    // DELETE (soft)
    // ===========================
    [HttpDelete("{loteId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int loteId)
    {
        var ok = await _svc.DeleteAsync(loteId);
        return ok ? NoContent() : NotFound();
    }

    // ===========================
    // DELETE (hard)
    // ===========================
    [HttpDelete("{loteId}/hard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HardDelete(int loteId)
    {
        var ok = await _svc.HardDeleteAsync(loteId);
        return ok ? NoContent() : NotFound();
    }

      /// <summary>
    /// Resumen de mortalidad de un lote de levante (acumulado y saldos).
    /// </summary>
    [HttpGet("{loteId}/resumen-mortalidad")]
    [ProducesResponseType(typeof(LoteMortalidadResumenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResumenMortalidad(int loteId)
    {
        var dto = await _svc.GetMortalidadResumenAsync(loteId);
        if (dto is null) return NotFound();
        return Ok(dto);
    }
}
