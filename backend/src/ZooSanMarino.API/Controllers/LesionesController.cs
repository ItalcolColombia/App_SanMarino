using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.DTOs.Lesiones;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Lesiones por lote (Panamá — tab dentro de Seguimiento Diario Reproductora/Apoyo/Engorde).
/// Una sola tabla cubre los tres módulos vía la columna modulo_origen.
/// </summary>
[ApiController]
[Route("api/lesiones")]
[Produces("application/json")]
public class LesionesController : ControllerBase
{
    private readonly ILesionService _service;

    public LesionesController(ILesionService service) => _service = service;

    /// <summary>Búsqueda paginada con filtros (modulo_origen, cliente, farm, lote, fechas).</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<LesionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LesionDto>>> Search(
        [FromQuery] string?   moduloOrigen      = null,
        [FromQuery] int?      clienteId         = null,
        [FromQuery] int?      farmId            = null,
        [FromQuery] string?   galponId          = null,
        [FromQuery] int?      loteId            = null,
        [FromQuery] string?   loteReproductoraId = null,
        [FromQuery] string?   tipoLesion        = null,
        [FromQuery] DateTime? fechaDesde        = null,
        [FromQuery] DateTime? fechaHasta        = null,
        [FromQuery] bool      soloActivos       = true,
        [FromQuery] string    sortBy            = "fecha_registro",
        [FromQuery] bool      sortDesc          = true,
        [FromQuery] int       page              = 1,
        [FromQuery] int       pageSize          = 20,
        CancellationToken ct = default)
    {
        var req = new LesionSearchRequest(moduloOrigen, clienteId, farmId, galponId, loteId,
            loteReproductoraId, tipoLesion, fechaDesde, fechaHasta, soloActivos, sortBy, sortDesc, page, pageSize);
        return Ok(await _service.SearchAsync(req, ct));
    }

    /// <summary>Obtiene una lesión por ID.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(LesionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LesionDto>> GetById(long id, CancellationToken ct)
    {
        var dto = await _service.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Registra una nueva lesión.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LesionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LesionDto>> Create([FromBody] CreateLesionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TipoLesion))
            return BadRequest("TipoLesion es requerido.");
        if (string.IsNullOrWhiteSpace(req.ModuloOrigen) ||
            (req.ModuloOrigen != "REPRODUCTORA" && req.ModuloOrigen != "APOYO" && req.ModuloOrigen != "ENGORDE"))
            return BadRequest("ModuloOrigen debe ser uno de: REPRODUCTORA, APOYO, ENGORDE.");
        if (req.FarmId <= 0)
            return BadRequest("FarmId es requerido.");

        var dto = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    /// <summary>Actualiza una lesión existente.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(LesionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LesionDto>> Update(long id, [FromBody] UpdateLesionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TipoLesion))
            return BadRequest("TipoLesion es requerido.");

        var dto = await _service.UpdateAsync(id, req, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Eliminación lógica.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var ok = await _service.DeleteAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Resumen agrupado por tipo de lesión y módulo de origen.</summary>
    [HttpGet("resumen")]
    [ProducesResponseType(typeof(IEnumerable<LesionResumenDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LesionResumenDto>>> GetResumen(
        [FromQuery] string? moduloOrigen       = null,
        [FromQuery] int?    clienteId          = null,
        [FromQuery] int?    farmId             = null,
        [FromQuery] int?    loteId             = null,
        [FromQuery] string? galponId           = null,
        [FromQuery] string? loteReproductoraId = null,
        CancellationToken ct = default)
    {
        var data = await _service.GetResumenAsync(
            moduloOrigen, clienteId, farmId, loteId, galponId, loteReproductoraId, ct);
        return Ok(data);
    }
}
