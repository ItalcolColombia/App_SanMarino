// src/ZooSanMarino.API/Controllers/LoteReproductoraAveEngordeController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using AvesDisponiblesDto = ZooSanMarino.Application.DTOs.AvesDisponiblesDto;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LoteReproductoraAveEngordeController : ControllerBase
{
    private readonly ILoteReproductoraAveEngordeService _svc;
    private readonly IServiceScopeFactory _scopeFactory;

    public LoteReproductoraAveEngordeController(ILoteReproductoraAveEngordeService svc, IServiceScopeFactory scopeFactory)
    {
        _svc = svc;
        _scopeFactory = scopeFactory;
    }

    [HttpGet("filter-data")]
    [ProducesResponseType(typeof(LoteReproductoraAveEngordeFilterDataDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoteReproductoraAveEngordeFilterDataDto>> GetFilterData(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var filterDataSvc = scope.ServiceProvider.GetRequiredService<ILoteReproductoraAveEngordeFilterDataService>();
        var data = await filterDataSvc.GetFilterDataAsync(ct);
        return Ok(data);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LoteReproductoraAveEngordeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LoteReproductoraAveEngordeDto>>> GetAll(
        [FromQuery] int? loteAveEngordeId,
        CancellationToken ct)
    {
        var items = await _svc.GetAllAsync(loteAveEngordeId);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LoteReproductoraAveEngordeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteReproductoraAveEngordeDto>> GetById([FromRoute] int id, CancellationToken ct)
    {
        var dto = await _svc.GetByIdAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [ProducesResponseType(typeof(LoteReproductoraAveEngordeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoteReproductoraAveEngordeDto>> Create(
        [FromBody] CreateLoteReproductoraAveEngordeDto dto,
        CancellationToken ct)
    {
        if (dto is null) return ValidationProblem(new ValidationProblemDetails { Detail = "Body requerido." });
        try
        {
            var created = await _svc.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(new ValidationProblemDetails { Detail = ex.Message });
        }
    }

    [HttpPost("bulk")]
    [ProducesResponseType(typeof(IEnumerable<LoteReproductoraAveEngordeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<LoteReproductoraAveEngordeDto>>> CreateBulk(
        [FromBody] IEnumerable<CreateLoteReproductoraAveEngordeDto> dtos,
        CancellationToken ct)
    {
        if (dtos is null) return ValidationProblem(new ValidationProblemDetails { Detail = "Body requerido." });
        try
        {
            var created = await _svc.CreateBulkAsync(dtos);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(new ValidationProblemDetails { Detail = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(LoteReproductoraAveEngordeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteReproductoraAveEngordeDto>> Update(
        [FromRoute] int id,
        [FromBody] UpdateLoteReproductoraAveEngordeDto dto,
        CancellationToken ct)
    {
        if (dto is null) return ValidationProblem(new ValidationProblemDetails { Detail = "Body requerido." });
        try
        {
            var updated = await _svc.UpdateAsync(id, dto);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(new ValidationProblemDetails { Detail = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
    {
        var ok = await _svc.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("new-code")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<string>> GetNewReproductoraCode(
        [FromQuery] int loteAveEngordeId,
        [FromQuery] string? exclude = null,
        CancellationToken ct = default)
    {
        try
        {
            var excludeList = string.IsNullOrWhiteSpace(exclude)
                ? null
                : exclude!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var code = await _svc.GetNewReproductoraCodeAsync(loteAveEngordeId, excludeList);
            return Ok(code);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpGet("{loteAveEngordeId:int}/aves-disponibles")]
    [ProducesResponseType(typeof(AvesDisponiblesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvesDisponiblesDto>> GetAvesDisponibles(
        [FromRoute] int loteAveEngordeId,
        CancellationToken ct)
    {
        var aves = await _svc.GetAvesDisponiblesAsync(loteAveEngordeId);
        return aves is null ? NotFound() : Ok(aves);
    }
}
