// src/ZooSanMarino.API/Controllers/VacunacionRegistroController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
[Tags("Vacunación — Registro de aplicación")]
public class VacunacionRegistroController : ControllerBase
{
    private readonly IVacunacionRegistroService _svc;
    private readonly ICurrentUser _current;

    public VacunacionRegistroController(IVacunacionRegistroService svc, ICurrentUser current)
    {
        _svc = svc;
        _current = current;
    }

    /// <summary>Confirma aplicado. La fecha de aplicación la fija el servidor (no viaja en el body).</summary>
    [HttpPost("{cronogramaItemId:int}/aplicar")]
    [ProducesResponseType(typeof(VacunacionCronogramaItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Aplicar(int cronogramaItemId, [FromBody] VacunacionRegistrarAplicadoRequest req, CancellationToken ct)
    {
        if (!_current.Permissions.Contains("vacunacion.registro.aplicar"))
            return Forbid();
        try
        {
            var dto = await _svc.RegistrarAplicadoAsync(cronogramaItemId, req, ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Marca no aplicado. Motivo obligatorio.</summary>
    [HttpPost("{cronogramaItemId:int}/no-aplicar")]
    [ProducesResponseType(typeof(VacunacionCronogramaItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> NoAplicar(int cronogramaItemId, [FromBody] VacunacionRegistrarNoAplicadoRequest req, CancellationToken ct)
    {
        if (!_current.Permissions.Contains("vacunacion.registro.aplicar"))
            return Forbid();
        try
        {
            var dto = await _svc.RegistrarNoAplicadoAsync(cronogramaItemId, req, ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
