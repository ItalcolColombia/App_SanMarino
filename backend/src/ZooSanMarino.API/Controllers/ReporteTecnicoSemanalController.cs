using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Reporte Técnico Semanal (Sanmarino postura): un módulo, dos reportes
/// (Levante 1-25 / Producción 25+) por lote base, comparados contra la guía
/// genética cargada de la empresa activa. Un tab por sublote (galpón) +
/// consolidado. La empresa sale de la sesión (ICurrentUser), no del body.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReporteTecnicoSemanalController : ControllerBase
{
    private readonly IReporteTecnicoSemanalService _service;

    public ReporteTecnicoSemanalController(IReporteTecnicoSemanalService service)
    {
        _service = service;
    }

    /// <summary>Reporte semanal de LEVANTE (semanas 1-25) del lote base.</summary>
    [HttpPost("levante")]
    [ProducesResponseType(typeof(ReporteTecnicoSemanalLevanteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReporteTecnicoSemanalLevanteResponse>> GenerarLevante(
        [FromBody] ReporteTecnicoSemanalRequest request, CancellationToken ct)
    {
        if (request is null || request.LotePosturaBaseId <= 0)
            return BadRequest(new { message = "LotePosturaBaseId es requerido." });

        try
        {
            return Ok(await _service.GenerarLevanteAsync(request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Reporte semanal de PRODUCCIÓN (semana 25+) del lote base.</summary>
    [HttpPost("produccion")]
    [ProducesResponseType(typeof(ReporteTecnicoSemanalProduccionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReporteTecnicoSemanalProduccionResponse>> GenerarProduccion(
        [FromBody] ReporteTecnicoSemanalRequest request, CancellationToken ct)
    {
        if (request is null || request.LotePosturaBaseId <= 0)
            return BadRequest(new { message = "LotePosturaBaseId es requerido." });

        try
        {
            return Ok(await _service.GenerarProduccionAsync(request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
