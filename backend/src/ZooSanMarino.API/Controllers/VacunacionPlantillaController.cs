// src/ZooSanMarino.API/Controllers/VacunacionPlantillaController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Plan de vacunación estándar de la empresa (W1.3).
///
/// <para>
/// Permisos propios (<c>vacunacion.plantillas.*</c>) y no los del cronograma: editar el plan de la
/// empresa alcanza a todos los lotes futuros, mientras que el cronograma alcanza a uno. La migración
/// se los da a quienes ya tenían los de cronograma, así que hoy la población es la misma; mañana se
/// pueden separar sin tocar código.
/// </para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
[Tags("Vacunación — Plantillas")]
public class VacunacionPlantillaController : ControllerBase
{
    private const string PermisoVer = "vacunacion.plantillas.ver";
    private const string PermisoAdministrar = "vacunacion.plantillas.administrar";

    private readonly IVacunacionPlantillaService _svc;
    private readonly ICurrentUser _current;

    public VacunacionPlantillaController(IVacunacionPlantillaService svc, ICurrentUser current)
    {
        _svc = svc;
        _current = current;
    }

    /// <summary>Quien puede administrar también puede ver, sin necesidad de tener las dos claves.</summary>
    private bool PuedeVer() => _current.Permissions.Contains(PermisoVer) || _current.Permissions.Contains(PermisoAdministrar);
    private bool PuedeAdministrar() => _current.Permissions.Contains(PermisoAdministrar);

    /// <summary>Plantillas de la empresa activa, con el conteo de vacunas de cada una.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<VacunacionPlantillaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? lineaProductiva, [FromQuery] bool soloActivas, CancellationToken ct)
    {
        if (!PuedeVer()) return Forbid();
        return Ok(await _svc.GetAllAsync(lineaProductiva, soloActivas, ct));
    }

    /// <summary>Plantilla con sus vacunas, en el orden en que se van a materializar.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(VacunacionPlantillaDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        if (!PuedeVer()) return Forbid();
        var dto = await _svc.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Qué plantilla le tocaría a un lote, y por qué. <b>Solo lectura</b>: no escribe cronograma.
    /// </summary>
    [HttpGet("efectiva")]
    [ProducesResponseType(typeof(VacunacionPlantillaEfectivaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEfectiva(
        [FromQuery] string lineaProductiva, [FromQuery] int loteId, CancellationToken ct)
    {
        if (!PuedeVer()) return Forbid();
        try
        {
            return Ok(await _svc.GetEfectivaAsync(lineaProductiva, loteId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(VacunacionPlantillaDetalleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] VacunacionPlantillaCreateRequest req, CancellationToken ct)
    {
        if (!PuedeAdministrar()) return Forbid();
        try
        {
            var dto = await _svc.CreateAsync(req, ct);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(VacunacionPlantillaDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(int id, [FromBody] VacunacionPlantillaUpdateRequest req, CancellationToken ct)
    {
        if (!PuedeAdministrar()) return Forbid();
        try
        {
            var dto = await _svc.UpdateAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Baja lógica de la plantilla y de sus vacunas (mismo sello de fecha).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!PuedeAdministrar()) return Forbid();
        return await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
    }

    [HttpPost("{id:int}/items")]
    [ProducesResponseType(typeof(VacunacionPlantillaItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddItem(int id, [FromBody] VacunacionPlantillaItemCreateRequest req, CancellationToken ct)
    {
        if (!PuedeAdministrar()) return Forbid();
        try
        {
            return Ok(await _svc.AddItemAsync(id, req, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}/items/{itemId:int}")]
    [ProducesResponseType(typeof(VacunacionPlantillaItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateItem(
        int id, int itemId, [FromBody] VacunacionPlantillaItemUpdateRequest req, CancellationToken ct)
    {
        if (!PuedeAdministrar()) return Forbid();
        try
        {
            var dto = await _svc.UpdateItemAsync(id, itemId, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}/items/{itemId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteItem(int id, int itemId, CancellationToken ct)
    {
        if (!PuedeAdministrar()) return Forbid();
        return await _svc.DeleteItemAsync(id, itemId, ct) ? NoContent() : NotFound();
    }
}
