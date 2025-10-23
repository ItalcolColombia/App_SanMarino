// file: src/ZooSanMarino.API/Controllers/FarmController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Application.DTOs;                     // FarmDto, CreateFarmDto, UpdateFarmDto
using CommonDtos = ZooSanMarino.Application.DTOs.Common; // PagedResult<T>
using ZooSanMarino.Application.DTOs.Farms;  // FarmDetailDto, FarmSearchRequest, FarmTreeDto

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Farms")]
public class FarmController : ControllerBase
{
    private readonly IFarmService _svc;
    public FarmController(IFarmService svc) => _svc = svc;

    // ===========================
    // LISTADO SIMPLE (compat)
    // ===========================
    // /api/Farm  (ruta original)
    // /Farm      (alias sin /api, para alinear con el front Angular)
    [HttpGet]
    [HttpGet("/Farm")]
    [ProducesResponseType(typeof(IEnumerable<FarmDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FarmDto>>> GetAll([FromQuery] Guid? id_user_session = null)
    {
        var items = await _svc.GetAllAsync(id_user_session);
        return Ok(items);
    }

    // ===========================
    // BÚSQUEDA AVANZADA
    // ===========================
    // /api/Farm/search
    // /Farm/search (alias opcional por compatibilidad)
    [HttpGet("search")]
    [HttpGet("/Farm/search")]
    [ProducesResponseType(typeof(CommonDtos.PagedResult<FarmDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CommonDtos.PagedResult<FarmDetailDto>>> Search([FromQuery] FarmSearchRequest req)
    {
        var res = await _svc.SearchAsync(req);
        return Ok(res);
    }

    // ===========================
    // DETALLE (incluye métricas)
    // ===========================
    // /api/Farm/{id}
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FarmDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmDetailDto>> GetDetail(int id)
    {
        var res = await _svc.GetDetailByIdAsync(id);
        if (res is null) return NotFound();
        return Ok(res);
    }

    // (Opcional) GET básico por id para compatibilidad con UIs antiguas
    // /api/Farm/{id}/basic
    [HttpGet("{id:int}/basic")]
    [ProducesResponseType(typeof(FarmDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmDto>> GetByIdBasic(int id)
    {
        var res = await _svc.GetByIdAsync(id);
        if (res is null) return NotFound();
        return Ok(res);
    }

    // ===========================
    // ÁRBOL (Farm → Núcleos → Galpones)
    // ===========================
    // /api/Farm/{id}/tree
    [HttpGet("{id:int}/tree")]
    [ProducesResponseType(typeof(FarmTreeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmTreeDto>> GetTree(int id, [FromQuery] bool soloActivos = true)
    {
        var res = await _svc.GetTreeByIdAsync(id, soloActivos);
        if (res is null) return NotFound();
        return Ok(res);
    }

    // ===========================
    // CREATE
    // ===========================
    // /api/Farm
    [HttpPost]
    [ProducesResponseType(typeof(FarmDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FarmDto>> Create([FromBody] CreateFarmDto dto)
    {
        if (dto is null) return BadRequest("Body requerido.");
        var created = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetDetail), new { id = created.Id }, created);
    }

    // ===========================
    // UPDATE
    // ===========================
    // /api/Farm/{id}
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(FarmDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmDto>> Update(int id, [FromBody] UpdateFarmDto dto)
    {
        if (dto is null) return BadRequest("Body requerido.");
        if (id != dto.Id) return BadRequest("El id de la ruta no coincide con el del cuerpo.");

        var updated = await _svc.UpdateAsync(dto);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    // ===========================
    // DELETE (soft)
    // ===========================
    // /api/Farm/{id}
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _svc.DeleteAsync(id);
        if (!ok) return NotFound();
        return NoContent();
    }

    // ===========================
    // DELETE (hard)
    // ===========================
    // /api/Farm/{id}/hard
    [HttpDelete("{id:int}/hard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HardDelete(int id)
    {
        var ok = await _svc.HardDeleteAsync(id);
        if (!ok) return NotFound();
        return NoContent();
    }
}
