using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Lotes;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LotePosturaLevanteController : ControllerBase
{
    private readonly ILotePosturaLevanteService _svc;

    public LotePosturaLevanteController(ILotePosturaLevanteService svc)
    {
        _svc = svc;
    }

    /// <summary>
    /// Obtiene todos los registros de lote_postura_levante de la empresa en sesión.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LotePosturaLevanteDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LotePosturaLevanteDetailDto>>> GetAll(CancellationToken ct = default)
    {
        var items = await _svc.GetAllAsync(ct);
        return Ok(items);
    }

    /// <summary>
    /// Obtiene los lotes levante asociados a un lote (lote_id).
    /// </summary>
    [HttpGet("por-lote/{loteId:int}")]
    [ProducesResponseType(typeof(IEnumerable<LotePosturaLevanteDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LotePosturaLevanteDetailDto>>> GetByLoteId(int loteId, CancellationToken ct = default)
    {
        var items = await _svc.GetByLoteIdAsync(loteId, ct);
        return Ok(items);
    }

    /// <summary>
    /// Obtiene el detalle de un lote levante por ID (incluye EdadMaximaSeguimiento).
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LotePosturaLevanteDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotePosturaLevanteDetailDto>> GetById(int id, CancellationToken ct = default)
    {
        var item = await _svc.GetByIdAsync(id, ct);
        if (item == null) return NotFound();
        return Ok(item);
    }

    /// <summary>Resumen para el modal «Cerrar lote» (aves H/M y si ya existe producción).</summary>
    [HttpGet("{id:int}/resumen-cierre")]
    [ProducesResponseType(typeof(CierreLoteLevanteResumenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CierreLoteLevanteResumenDto>> GetResumenCierre(int id, CancellationToken ct = default)
    {
        var r = await _svc.GetResumenCierreAsync(id, ct);
        if (r == null) return NotFound();
        return Ok(r);
    }

    /// <summary>Cierra el lote levante y crea el lote de producción con huevos iniciales indicados.</summary>
    [HttpPost("{id:int}/cerrar")]
    [ProducesResponseType(typeof(LotePosturaLevanteDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotePosturaLevanteDetailDto>> Cerrar(int id, [FromBody] CerrarLoteLevanteRequest? body, CancellationToken ct = default)
    {
        try
        {
            if (body is null) return BadRequest(new { message = "Body requerido (huevosIniciales, closedByUserId)." });
            var res = await _svc.CerrarLoteYCrearProduccionAsync(id, body, ct);
            if (res == null) return NotFound();
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

    /// <summary>Reabre el lote levante (solo si el lote producción no tiene registros dependientes).</summary>
    [HttpPost("{id:int}/abrir")]
    [ProducesResponseType(typeof(LotePosturaLevanteDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotePosturaLevanteDetailDto>> Abrir(int id, [FromBody] AbrirLoteLevanteRequest? body, CancellationToken ct = default)
    {
        try
        {
            if (body is null) return BadRequest(new { message = "Body requerido." });
            var res = await _svc.AbrirLoteAsync(id, body, ct);
            if (res == null) return NotFound();
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
