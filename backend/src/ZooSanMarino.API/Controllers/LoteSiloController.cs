using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// De qué silos consume un LOTE. El seguimiento diario (levante y producción) solo ofrece estos, y
/// rechaza un consumo contra un silo que no esté acá.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LoteSiloController : ControllerBase
{
    private readonly ILoteSiloService _svc;
    public LoteSiloController(ILoteSiloService svc) => _svc = svc;

    /// <summary>Silos asignados al lote. <c>loteId</c> es el id del lote MAESTRO (<c>lotes.lote_id</c>).</summary>
    [HttpGet("{loteId:int}")]
    [ProducesResponseType(typeof(IEnumerable<LoteSiloDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<LoteSiloDto>>> GetByLote(int loteId, CancellationToken ct = default)
    {
        try { return Ok(await _svc.GetByLoteAsync(loteId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Silos elegibles para el lote: los de su galpón. Si el galpón no tiene ninguno asignado,
    /// devuelve todos los activos de la granja (para no dejar al lote sin de dónde consumir).
    /// </summary>
    [HttpGet("{loteId:int}/disponibles")]
    [ProducesResponseType(typeof(IEnumerable<FarmSiloDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<FarmSiloDto>>> GetDisponibles(int loteId, CancellationToken ct = default)
    {
        try { return Ok(await _svc.GetDisponiblesAsync(loteId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Reemplaza el conjunto de silos del lote. Lista vacía = ninguno.</summary>
    [HttpPut("{loteId:int}")]
    [ProducesResponseType(typeof(IEnumerable<LoteSiloDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<LoteSiloDto>>> Asignar(int loteId, [FromBody] AsignarSilosDto dto, CancellationToken ct = default)
    {
        if (dto is null) return BadRequest(new { message = "El cuerpo de la petición es requerido." });
        try { return Ok(await _svc.AsignarAsync(loteId, dto, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
