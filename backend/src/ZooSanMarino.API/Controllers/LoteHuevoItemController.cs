using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// F7.3 — qué tipos de huevo produce un LOTE. El seguimiento diario de producción muestra una fila
/// fija por cada uno y rechaza cualquier ítem que no esté acá (fail-closed: sin declaración, no se
/// puede clasificar).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LoteHuevoItemController : ControllerBase
{
    private readonly ILoteHuevoItemService _svc;
    public LoteHuevoItemController(ILoteHuevoItemService svc) => _svc = svc;

    /// <summary>Tipos de huevo declarados por el lote. <c>loteId</c> es <c>lotes.lote_id</c> (el maestro).</summary>
    [HttpGet("{loteId:int}")]
    [ProducesResponseType(typeof(IEnumerable<LoteHuevoItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<LoteHuevoItemDto>>> GetByLote(int loteId, CancellationToken ct = default)
    {
        try { return Ok(await _svc.GetByLoteAsync(loteId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Ítems de huevo elegibles: el catálogo ACTIVO de la empresa dueña de la granja del lote, con
    /// <c>activo=true</c> en los que el lote ya declaró (para que el modal marque los tildados).
    /// </summary>
    [HttpGet("{loteId:int}/disponibles")]
    [ProducesResponseType(typeof(IEnumerable<LoteHuevoItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<LoteHuevoItemDto>>> GetDisponibles(int loteId, CancellationToken ct = default)
    {
        try { return Ok(await _svc.GetDisponiblesAsync(loteId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Ítems de huevo elegibles para un lote que TODAVÍA NO EXISTE, resueltos por la granja elegida
    /// en el formulario de alta. Ninguno viene marcado: no hay declaración previa.
    /// </summary>
    [HttpGet("por-granja/{granjaId:int}/disponibles")]
    [ProducesResponseType(typeof(IEnumerable<LoteHuevoItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<LoteHuevoItemDto>>> GetDisponiblesPorGranja(
        int granjaId, CancellationToken ct = default)
    {
        try { return Ok(await _svc.GetDisponiblesPorGranjaAsync(granjaId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Reemplaza el conjunto de tipos de huevo del lote. Lista vacía = ninguno.</summary>
    [HttpPut("{loteId:int}")]
    [ProducesResponseType(typeof(IEnumerable<LoteHuevoItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<LoteHuevoItemDto>>> Asignar(
        int loteId, [FromBody] AsignarHuevoItemsDto dto, CancellationToken ct = default)
    {
        if (dto is null) return BadRequest(new { message = "El cuerpo de la petición es requerido." });
        try { return Ok(await _svc.AsignarAsync(loteId, dto, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
