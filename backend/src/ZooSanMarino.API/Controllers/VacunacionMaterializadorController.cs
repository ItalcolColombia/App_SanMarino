// src/ZooSanMarino.API/Controllers/VacunacionMaterializadorController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Bajar el plan de vacunación de la empresa al cronograma de los lotes (W2).
///
/// <para>
/// Cada <c>preview</c> y su <c>aplicar</c> devuelven el <b>mismo</b> informe, calculado por la misma
/// función pura: lo que se ve antes de confirmar es lo que se escribe. Aplicar es idempotente —correr
/// dos veces no duplica— y nunca borra una fila del cronograma.
/// </para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
[Tags("Vacunación — Materializador")]
public class VacunacionMaterializadorController : ControllerBase
{
    private const string PermisoPlantillasVer = "vacunacion.plantillas.ver";
    private const string PermisoPlantillasAdministrar = "vacunacion.plantillas.administrar";
    private const string PermisoCronogramaAdministrar = "vacunacion.cronograma.administrar";

    private readonly IVacunacionMaterializadorService _svc;
    private readonly ICurrentUser _current;

    public VacunacionMaterializadorController(IVacunacionMaterializadorService svc, ICurrentUser current)
    {
        _svc = svc;
        _current = current;
    }

    private bool PuedeVer() =>
        _current.Permissions.Contains(PermisoPlantillasVer) || _current.Permissions.Contains(PermisoPlantillasAdministrar);

    /// <summary>
    /// Escribir exige las <b>dos</b> claves, porque la acción es las dos cosas a la vez: leer el plan
    /// de la empresa y escribir el cronograma de N lotes. Hoy la población es idéntica —la migración
    /// de W1.3 le dio <c>plantillas.administrar</c> exactamente a los roles que ya tenían
    /// <c>cronograma.administrar</c>—, así que nadie gana ni pierde acceso; mañana la distinción existe.
    /// </summary>
    private bool PuedeAplicar() =>
        _current.Permissions.Contains(PermisoPlantillasAdministrar)
        && _current.Permissions.Contains(PermisoCronogramaAdministrar);

    /// <summary>Qué pasaría con el cronograma de un lote si se le aplicara su plan. No escribe nada.</summary>
    [HttpGet("preview")]
    [ProducesResponseType(typeof(VacunacionMaterializacionLoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Preview(
        [FromQuery] string lineaProductiva, [FromQuery] int loteId, CancellationToken ct)
    {
        if (!PuedeVer()) return Forbid();
        try
        {
            return Ok(await _svc.PreviewLoteAsync(lineaProductiva, loteId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Aplica el plan a un lote. Correrlo de nuevo no escribe nada.</summary>
    [HttpPost("lote")]
    [ProducesResponseType(typeof(VacunacionMaterializacionLoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AplicarLote([FromBody] VacunacionMaterializarLoteRequest req, CancellationToken ct)
    {
        if (!PuedeAplicar()) return Forbid();
        try
        {
            return Ok(await _svc.AplicarLoteAsync(req.LineaProductiva, req.LoteId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Qué pasaría con todos los lotes abiertos a los que hoy les toca esta plantilla. No escribe nada.
    /// </summary>
    [HttpGet("preview-masivo")]
    [ProducesResponseType(typeof(VacunacionMaterializacionMasivaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PreviewMasivo([FromQuery] int plantillaId, CancellationToken ct)
    {
        if (!PuedeVer()) return Forbid();
        try
        {
            return Ok(await _svc.PreviewPlantillaAsync(plantillaId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Aplica la plantilla a todos los lotes abiertos que resuelven a ella, uno por transacción: el
    /// que falle queda reportado con su error y los demás se aplican igual.
    /// </summary>
    [HttpPost("plantilla/{id:int}/aplicar")]
    [ProducesResponseType(typeof(VacunacionMaterializacionMasivaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AplicarPlantilla(int id, CancellationToken ct)
    {
        if (!PuedeAplicar()) return Forbid();
        try
        {
            return Ok(await _svc.AplicarPlantillaAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

/// <summary>Lote al que aplicarle su plan. Va por body y no por query porque escribe.</summary>
public record VacunacionMaterializarLoteRequest(string LineaProductiva, int LoteId);
