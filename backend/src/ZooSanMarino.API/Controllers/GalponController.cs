// file: src/ZooSanMarino.API/Controllers/GalponController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Application.DTOs;                      // CreateGalponDto, UpdateGalponDto
using CommonDtos = ZooSanMarino.Application.DTOs.Common; // PagedResult<T>
using GalponDtos = ZooSanMarino.Application.DTOs.Galpones; // GalponDetailDto, GalponSearchRequest

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GalponController : ControllerBase
{
    private readonly IGalponService _svc;
    public GalponController(IGalponService svc) => _svc = svc;

    // ─────────────────────────────────────────────────────────────────────────────
    // LISTADOS
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Listado (detalle) de galpones activos de la compañía actual.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<GalponDtos.GalponDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GalponDtos.GalponDetailDto>>> GetAll()
    {
        var items = await _svc.GetAllAsync();
        return Ok(items);
    }

    /// <summary>Listado (detalle) completo; alias explícito de detalle.</summary>
    [HttpGet("detail")]
    [ProducesResponseType(typeof(IEnumerable<GalponDtos.GalponDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GalponDtos.GalponDetailDto>>> GetAllDetail()
    {
        var items = await _svc.GetAllDetailAsync();
        return Ok(items);
    }

    /// <summary>Búsqueda paginada.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(CommonDtos.PagedResult<GalponDtos.GalponDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CommonDtos.PagedResult<GalponDtos.GalponDetailDto>>> Search([FromQuery] GalponDtos.GalponSearchRequest req)
    {
        var res = await _svc.SearchAsync(req);
        return Ok(res);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // QUERIES POR ID / FILTROS
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Detalle por Id (incluye Farm, Nucleo, Company).</summary>
    [HttpGet("{galponId}")]
    [ProducesResponseType(typeof(GalponDtos.GalponDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GalponDtos.GalponDetailDto>> GetById(string galponId)
    {
        var res = await _svc.GetByIdAsync(galponId);
        if (res is null) return NotFound();
        return Ok(res);
    }

    /// <summary>Detalle por Id (ruta explícita).</summary>
    [HttpGet("{galponId}/detail")]
    [ProducesResponseType(typeof(GalponDtos.GalponDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GalponDtos.GalponDetailDto>> GetDetailById(string galponId)
    {
        var res = await _svc.GetDetailByIdAsync(galponId);
        if (res is null) return NotFound();
        return Ok(res);
    }

    /// <summary>Detalle simple por Id (misma data estructural, atajo opcional).</summary>
    [HttpGet("{galponId}/detail-simple")]
    [ProducesResponseType(typeof(GalponDtos.GalponDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GalponDtos.GalponDetailDto>> GetDetailByIdSimple(string galponId)
    {
        var res = await _svc.GetDetailByIdSimpleAsync(galponId);
        if (res is null) return NotFound();
        return Ok(res);
    }

    /// <summary>Listado por granja (detalle).</summary>
    [HttpGet("granja/{granjaId:int}")]
    [ProducesResponseType(typeof(IEnumerable<GalponDtos.GalponDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GalponDtos.GalponDetailDto>>> GetByGranja(int granjaId)
    {
        var items = await _svc.GetByGranjaAsync(granjaId);
        return Ok(items);
    }

    /// <summary>Listado por granja y núcleo (detalle).</summary>
    [HttpGet("granja/{granjaId:int}/nucleo/{nucleoId}")]
    [ProducesResponseType(typeof(IEnumerable<GalponDtos.GalponDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GalponDtos.GalponDetailDto>>> GetByGranjaAndNucleo(int granjaId, string nucleoId)
    {
        var items = await _svc.GetByGranjaAndNucleoAsync(granjaId, nucleoId);
        return Ok(items);
    }

    /// <summary>Listado por granja y núcleo (detalle) — ruta explícita.</summary>
    [HttpGet("granja/{granjaId:int}/nucleo/{nucleoId}/detail")]
    [ProducesResponseType(typeof(IEnumerable<GalponDtos.GalponDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GalponDtos.GalponDetailDto>>> GetDetailByGranjaAndNucleo(int granjaId, string nucleoId)
    {
        var items = await _svc.GetDetailByGranjaAndNucleoAsync(granjaId, nucleoId);
        return Ok(items);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // CREATE / UPDATE / DELETE
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Crea un galpón y retorna su detalle.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(GalponDtos.GalponDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GalponDtos.GalponDetailDto>> Create([FromBody] CreateGalponDto dto)
    {
        if (dto is null) return BadRequest("Body requerido.");
        var created = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { galponId = created.GalponId }, created);
    }

    /// <summary>Actualiza un galpón y retorna su detalle.</summary>
    [HttpPut("{galponId}")]
    [ProducesResponseType(typeof(GalponDtos.GalponDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GalponDtos.GalponDetailDto>> Update(string galponId, [FromBody] UpdateGalponDto dto)
    {
        if (dto is null) return BadRequest("Body requerido.");
        if (!string.Equals(dto.GalponId, galponId, StringComparison.OrdinalIgnoreCase))
            return BadRequest("El id de la ruta no coincide con el del cuerpo.");

        var updated = await _svc.UpdateAsync(dto);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    /// <summary>Elimina (soft delete) un galpón.</summary>
    [HttpDelete("{galponId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string galponId)
    {
        var ok = await _svc.DeleteAsync(galponId);
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>Elimina físicamente (hard delete) un galpón.</summary>
    [HttpDelete("{galponId}/hard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HardDelete(string galponId)
    {
        var ok = await _svc.HardDeleteAsync(galponId);
        if (!ok) return NotFound();
        return NoContent();
    }
}
