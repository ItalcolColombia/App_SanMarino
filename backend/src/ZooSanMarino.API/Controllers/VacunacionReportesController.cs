// src/ZooSanMarino.API/Controllers/VacunacionReportesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
[Tags("Vacunación — Reportes de cumplimiento")]
public class VacunacionReportesController : ControllerBase
{
    private readonly IVacunacionReportesService _svc;
    private readonly ICurrentUser _current;

    public VacunacionReportesController(IVacunacionReportesService svc, ICurrentUser current)
    {
        _svc = svc;
        _current = current;
    }

    /// <summary>Cumplimiento por lote: % a tiempo / tardío (leve / incumplido) / no aplicado + promedio de días de atraso.</summary>
    [HttpPost("cumplimiento")]
    [ProducesResponseType(typeof(List<VacunacionCumplimientoLoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCumplimiento([FromBody] VacunacionCumplimientoFiltroRequest req, CancellationToken ct)
    {
        if (!_current.Permissions.Contains("vacunacion.reportes.ver"))
            return Forbid();
        var data = await _svc.GetCumplimientoAsync(req, ct);
        return Ok(data);
    }
}
