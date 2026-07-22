// src/ZooSanMarino.API/Controllers/LoteBaseEngordeController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.LoteBaseEngorde;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Catálogo de lotes base de pollo engorde (agrupador global por empresa).
/// Se amarra opcionalmente a lote_ave_engorde para el Reporte Diario Costos por granja.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LoteBaseEngordeController : ControllerBase
{
    private readonly ILoteBaseEngordeService _svc;

    public LoteBaseEngordeController(ILoteBaseEngordeService svc) => _svc = svc;

    /// <summary>Lista los lotes base vivos de la empresa efectiva.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LoteBaseEngordeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LoteBaseEngordeDto>>> GetAll(CancellationToken ct) =>
        Ok(await _svc.GetAllAsync(ct));

    /// <summary>Crea un lote base (nombre único por empresa).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LoteBaseEngordeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoteBaseEngordeDto>> Create([FromBody] CreateLoteBaseEngordeDto dto, CancellationToken ct)
    {
        try { return Ok(await _svc.CreateAsync(dto, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Actualiza nombre/descripción del lote base.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(LoteBaseEngordeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteBaseEngordeDto>> Update(int id, [FromBody] UpdateLoteBaseEngordeDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest(new { message = "El id de la ruta no coincide con el del cuerpo." });
        try
        {
            var res = await _svc.UpdateAsync(dto, ct);
            return res is null ? NotFound() : Ok(res);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Soft-delete. Bloqueado si tiene lotes de engorde vivos amarrados.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.DeleteAsync(id, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
