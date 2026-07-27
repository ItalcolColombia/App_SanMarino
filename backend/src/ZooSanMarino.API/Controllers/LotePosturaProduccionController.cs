using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Lotes;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LotePosturaProduccionController : ControllerBase
{
    private readonly ILotePosturaProduccionService _svc;

    public LotePosturaProduccionController(ILotePosturaProduccionService svc)
    {
        _svc = svc;
    }

    /// <summary>
    /// Obtiene todos los registros de lote_postura_produccion de la empresa en sesión.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LotePosturaProduccionDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LotePosturaProduccionDetailDto>>> GetAll([FromQuery] bool paraDestino = false, CancellationToken ct = default)
    {
        var items = await _svc.GetAllAsync(ct, paraDestino);
        return Ok(items);
    }

    /// <summary>
    /// Obtiene los lotes producción asociados a un lote (vía levante).
    /// </summary>
    [HttpGet("por-lote/{loteId:int}")]
    [ProducesResponseType(typeof(IEnumerable<LotePosturaProduccionDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LotePosturaProduccionDetailDto>>> GetByLoteId(int loteId, CancellationToken ct = default)
    {
        var items = await _svc.GetByLoteIdAsync(loteId, ct);
        return Ok(items);
    }

    /// <summary>
    /// Feature 14 — obtiene un LPP por ID con datos frescos (aves actuales + acumulados de traslado + loteId).
    /// Usado al abrir el modal de traslado para mostrar el saldo real.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LotePosturaProduccionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotePosturaProduccionDetailDto>> GetById(int id, CancellationToken ct = default)
    {
        var item = await _svc.GetByIdAsync(id, ct);
        if (item is null) return NotFound();
        return Ok(item);
    }

    /// <summary>Resumen para el modal «Cerrar/Abrir lote» de Seguimiento Diario de Producción.</summary>
    [HttpGet("{id:int}/resumen-cierre")]
    [ProducesResponseType(typeof(CierreLoteProduccionResumenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CierreLoteProduccionResumenDto>> GetResumenCierre(int id, CancellationToken ct = default)
    {
        var r = await _svc.GetResumenCierreAsync(id, ct);
        if (r is null) return NotFound();
        return Ok(r);
    }

    /// <summary>
    /// Cierra el lote de producción: bloquea crear, editar y eliminar seguimiento diario de ese
    /// lote. No borra ni modifica registros existentes y es reversible con <c>{id}/abrir</c>.
    /// </summary>
    [HttpPost("{id:int}/cerrar")]
    [ProducesResponseType(typeof(LotePosturaProduccionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotePosturaProduccionDetailDto>> Cerrar(int id, [FromBody] CerrarLoteProduccionRequest? body, CancellationToken ct = default)
    {
        try
        {
            if (body is null) return BadRequest(new { message = "Body requerido (motivo, closedByUserId)." });
            var res = await _svc.CerrarLoteAsync(id, body, ct);
            if (res is null) return NotFound();
            return Ok(res);
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

    /// <summary>Reabre un lote de producción cerrado y vuelve a habilitar la captura diaria.</summary>
    [HttpPost("{id:int}/abrir")]
    [ProducesResponseType(typeof(LotePosturaProduccionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotePosturaProduccionDetailDto>> Abrir(int id, [FromBody] AbrirLoteProduccionRequest? body, CancellationToken ct = default)
    {
        try
        {
            if (body is null) return BadRequest(new { message = "Body requerido (motivo, openedByUserId)." });
            var res = await _svc.AbrirLoteAsync(id, body, ct);
            if (res is null) return NotFound();
            return Ok(res);
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
