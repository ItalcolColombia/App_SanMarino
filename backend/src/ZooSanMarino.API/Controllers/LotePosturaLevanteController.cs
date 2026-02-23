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
}
