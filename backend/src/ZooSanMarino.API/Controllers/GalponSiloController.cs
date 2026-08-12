using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Qué silos alimentan a un galpón (N:M). Es navegación: define qué silos ofrecerle al usuario
/// cuando filtra por ese galpón, no dónde está el stock (el stock vive en el silo, de la granja).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GalponSiloController : ControllerBase
{
    private readonly IGalponSiloService _svc;
    public GalponSiloController(IGalponSiloService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<GalponSiloDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<GalponSiloDto>>> Get(
        [FromQuery] int granjaId,
        [FromQuery] string? nucleoId = null,
        [FromQuery] string? galponId = null,
        CancellationToken ct = default)
    {
        try { return Ok(await _svc.GetAsync(granjaId, nucleoId, galponId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Silos de la granja que se pueden asignar a un galpón.</summary>
    [HttpGet("disponibles")]
    [ProducesResponseType(typeof(IEnumerable<FarmSiloDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<FarmSiloDto>>> GetDisponibles([FromQuery] int granjaId, CancellationToken ct = default)
    {
        try { return Ok(await _svc.GetDisponiblesAsync(granjaId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Reemplaza el conjunto de silos del galpón. Lista vacía = ninguno.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(IEnumerable<GalponSiloDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<GalponSiloDto>>> Asignar(
        [FromQuery] int granjaId,
        [FromQuery] string nucleoId,
        [FromQuery] string galponId,
        [FromBody] AsignarSilosDto dto,
        CancellationToken ct = default)
    {
        if (dto is null) return BadRequest(new { message = "El cuerpo de la petición es requerido." });
        try { return Ok(await _svc.AsignarAsync(granjaId, nucleoId, galponId, dto, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
