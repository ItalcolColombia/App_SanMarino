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
    public async Task<ActionResult<IEnumerable<LotePosturaProduccionDetailDto>>> GetAll(CancellationToken ct = default)
    {
        var items = await _svc.GetAllAsync(ct);
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
}
