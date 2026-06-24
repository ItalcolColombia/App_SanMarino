using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Informe Semanal Pollo de Engorde (Panamá). Filtros: granja (todas/varias/una), núcleo, galpón, lote y semana (rango de fechas).
/// Datos reales desde seguimiento_diario_aves_engorde + movimiento_pollo_engorde (vía fn_informe_semanal_pollo_engorde).
/// Las columnas "Tabla" (guía genética) llegan vacías: aún no se conectan.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InformeSemanalPolloEngordeController : ControllerBase
{
    private readonly IInformeSemanalPolloEngordeService _service;
    private readonly ILogger<InformeSemanalPolloEngordeController> _logger;

    public InformeSemanalPolloEngordeController(
        IInformeSemanalPolloEngordeService service,
        ILogger<InformeSemanalPolloEngordeController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Genera el informe semanal según filtros. CompanyId se toma del usuario autenticado.</summary>
    [HttpPost("generar")]
    public async Task<ActionResult<InformeSemanalReporteDto>> Generar(
        [FromBody] InformeSemanalRequest request,
        CancellationToken ct)
    {
        try
        {
            var resultado = await _service.GenerarAsync(request ?? new InformeSemanalRequest(null, null, null, null, null, null), ct);
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar el Informe Semanal Pollo de Engorde. Request: {@Request}", request);
            return StatusCode(500, new { error = "Error interno al generar el informe semanal", message = ex.Message });
        }
    }
}
