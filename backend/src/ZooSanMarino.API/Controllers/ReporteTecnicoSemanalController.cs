using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.Calculos;
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

    /// <summary>
    /// Hoja «RESUMEN SEMANAL» del Informe RA Pesadas: una fila por lote para UNA
    /// semana calendario, en la etapa pedida ("levante" | "produccion").
    /// La semana del año usa la convención WEEKNUM de Excel (arranca en domingo),
    /// no la semana ISO.
    /// </summary>
    [HttpPost("resumen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerarResumen(
        [FromBody] ResumenSemanalRaPesadasRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "El cuerpo de la solicitud es requerido." });

        var etapa = ResumenSemanalRaPesadasCalculos.NormalizarEtapa(request.Etapa);
        if (etapa is null)
            return BadRequest(new { message = "Etapa inválida. Use 'levante' o 'produccion'." });

        try
        {
            return etapa == ResumenSemanalRaPesadasCalculos.EtapaLevante
                ? Ok(await _service.GenerarResumenLevanteAsync(request, ct))
                : Ok(await _service.GenerarResumenProduccionAsync(request, ct));
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
