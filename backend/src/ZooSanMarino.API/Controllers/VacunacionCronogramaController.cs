// src/ZooSanMarino.API/Controllers/VacunacionCronogramaController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
[Tags("Vacunación — Cronograma")]
public class VacunacionCronogramaController : ControllerBase
{
    private readonly IVacunacionCronogramaService _svc;
    private readonly ICurrentUser _current;

    public VacunacionCronogramaController(IVacunacionCronogramaService svc, ICurrentUser current)
    {
        _svc = svc;
        _current = current;
    }

    /// <summary>Granjas asignadas + lotes de las 3 líneas + vacunas del catálogo, para armar los combos del formulario.</summary>
    [HttpGet("filter-data")]
    [ProducesResponseType(typeof(VacunacionFilterDataDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFilterData(CancellationToken ct)
    {
        var data = await _svc.GetFilterDataAsync(ct);
        return Ok(data);
    }

    /// <summary>Cronograma completo del lote (encadena Levante↔Producción cuando corresponde).</summary>
    [HttpGet("por-lote")]
    [ProducesResponseType(typeof(List<VacunacionCronogramaItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCronogramaLote(
        [FromQuery] string lineaProductiva, [FromQuery] int loteId, CancellationToken ct)
    {
        try
        {
            var items = await _svc.GetCronogramaLoteAsync(new VacunacionCronogramaLoteRequest(lineaProductiva, loteId), ct);
            return Ok(items);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(VacunacionCronogramaItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] VacunacionCronogramaItemCreateRequest req, CancellationToken ct)
    {
        if (!_current.Permissions.Contains("vacunacion.cronograma.administrar"))
            return Forbid();
        try
        {
            var dto = await _svc.CreateAsync(req, ct);
            return CreatedAtAction(nameof(GetCronogramaLote), new { lineaProductiva = dto.LineaProductiva, loteId = dto.LoteId }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(VacunacionCronogramaItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(int id, [FromBody] VacunacionCronogramaItemUpdateRequest req, CancellationToken ct)
    {
        if (!_current.Permissions.Contains("vacunacion.cronograma.administrar"))
            return Forbid();
        try
        {
            var dto = await _svc.UpdateAsync(id, req, ct);
            if (dto is null) return NotFound();
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!_current.Permissions.Contains("vacunacion.cronograma.administrar"))
            return Forbid();
        var ok = await _svc.DeleteAsync(id, ct);
        if (!ok) return NotFound();
        return NoContent();
    }
}
