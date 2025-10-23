// file: src/ZooSanMarino.API/Controllers/NucleoController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Application.DTOs;                     // NucleoDto, CreateNucleoDto, UpdateNucleoDto
using CommonDtos = ZooSanMarino.Application.DTOs.Common; // PagedResult<T>
using NucleoDtos = ZooSanMarino.Application.DTOs.Nucleos; // NucleoDetailDto, NucleoSearchRequest

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NucleoController : ControllerBase
{
    private readonly INucleoService _svc;
    public NucleoController(INucleoService svc) => _svc = svc;

    // ===========================
    // LISTADO SIMPLE (compat)
    // ===========================
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NucleoDto>>> GetAll()
    {
        var items = await _svc.GetAllAsync();
        return Ok(items);
    }

    // ===========================
    // BÚSQUEDA AVANZADA
    // ===========================
    [HttpGet("search")]
    public async Task<ActionResult<CommonDtos.PagedResult<NucleoDtos.NucleoDetailDto>>> Search(
        [FromQuery] NucleoDtos.NucleoSearchRequest req)
    {
        var res = await _svc.SearchAsync(req);
        return Ok(res);
    }

    // ===========================
    // LISTAR POR GRANJA (compat)
    // ===========================
    [HttpGet("granja/{granjaId:int}")]
    public async Task<ActionResult<IEnumerable<NucleoDto>>> GetByGranja(int granjaId)
    {
        var items = await _svc.GetByGranjaAsync(granjaId);
        return Ok(items);
    }

    // ===========================
    // DETALLE (incluye métricas)
    // ===========================
    [HttpGet("{nucleoId}/{granjaId:int}")]
    public async Task<ActionResult<NucleoDtos.NucleoDetailDto>> GetDetail(string nucleoId, int granjaId)
    {
        var res = await _svc.GetDetailByIdAsync(nucleoId, granjaId);
        if (res is null) return NotFound();
        return Ok(res);
    }

    // (Opcional) GET básico por PK compuesta
    [HttpGet("{nucleoId}/{granjaId:int}/basic")]
    public async Task<ActionResult<NucleoDto>> GetByIdBasic(string nucleoId, int granjaId)
    {
        var res = await _svc.GetByIdAsync(nucleoId, granjaId);
        if (res is null) return NotFound();
        return Ok(res);
    }

    // ===========================
    // CREATE
    // ===========================
    [HttpPost]
    public async Task<ActionResult<NucleoDto>> Create([FromBody] CreateNucleoDto dto)
    {
        if (dto is null) return BadRequest("Body requerido.");

        var created = await _svc.CreateAsync(dto);
        // Apuntamos al detalle
        return CreatedAtAction(nameof(GetDetail),
            new { nucleoId = created.NucleoId, granjaId = created.GranjaId },
            created);
    }

    // ===========================
    // UPDATE
    // ===========================
    [HttpPut("{nucleoId}/{granjaId:int}")]
    public async Task<ActionResult<NucleoDto>> Update(string nucleoId, int granjaId, [FromBody] UpdateNucleoDto dto)
    {
        if (dto is null) return BadRequest("Body requerido.");
        if (!string.Equals(dto.NucleoId, nucleoId, StringComparison.OrdinalIgnoreCase) || dto.GranjaId != granjaId)
            return BadRequest("La clave de la ruta no coincide con el cuerpo.");

        var updated = await _svc.UpdateAsync(dto);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    // ===========================
    // DELETE (soft)
    // ===========================
    [HttpDelete("{nucleoId}/{granjaId:int}")]
    public async Task<IActionResult> Delete(string nucleoId, int granjaId)
    {
        var ok = await _svc.DeleteAsync(nucleoId, granjaId);
        if (!ok) return NotFound();
        return NoContent();
    }

    // ===========================
    // DELETE (hard) opcional
    // ===========================
    [HttpDelete("{nucleoId}/{granjaId:int}/hard")]
    public async Task<IActionResult> HardDelete(string nucleoId, int granjaId)
    {
        var ok = await _svc.HardDeleteAsync(nucleoId, granjaId);
        if (!ok) return NotFound();
        return NoContent();
    }
}
