// file: src/ZooSanMarino.API/Controllers/LoteReproductoraController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using AvesDisponiblesDto = ZooSanMarino.Application.DTOs.AvesDisponiblesDto;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LoteReproductoraController : ControllerBase
{
    private readonly ILoteReproductoraService _svc;
    private readonly IServiceScopeFactory _scopeFactory;

    public LoteReproductoraController(ILoteReproductoraService svc, IServiceScopeFactory scopeFactory)
    {
        _svc = svc;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Datos para los filtros en cascada (Granja → Núcleo → Galpón → Lote) en una sola llamada.
    /// Usa un scope nuevo para evitar concurrencia con el DbContext del request.
    /// </summary>
    [HttpGet("filter-data")]
    [ProducesResponseType(typeof(LoteReproductoraFilterDataDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoteReproductoraFilterDataDto>> GetFilterData(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var filterDataSvc = scope.ServiceProvider.GetRequiredService<ILoteReproductoraFilterDataService>();
        var data = await filterDataSvc.GetFilterDataAsync(ct);
        return Ok(data);
    }

    // ======================================
    // LISTADO (opcionalmente filtrado por lote)
    // GET /api/LoteReproductora?loteId=L001
    // ======================================
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LoteReproductoraDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LoteReproductoraDto>>> GetAll(
        [FromQuery] string? loteId,
        CancellationToken ct)
    {
        var items = await _svc.GetAllAsync(loteId);
        return Ok(items);
    }

    // ======================================
    // DETALLE
    // ======================================
    [HttpGet("{loteId}/{repId}")]
    [ProducesResponseType(typeof(LoteReproductoraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteReproductoraDto>> GetById(
        [FromRoute] string loteId,
        [FromRoute] string repId,
        CancellationToken ct)
    {
        var dto = await _svc.GetByIdAsync(loteId, repId);
        return dto is null ? NotFound() : Ok(dto);
    }

    // ======================================
    // CREAR (uno)
    // ======================================
    [HttpPost]
    [ProducesResponseType(typeof(LoteReproductoraDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoteReproductoraDto>> Create(
        [FromBody] CreateLoteReproductoraDto dto,
        CancellationToken ct)
    {
        if (dto is null)
            return ValidationProblem(new ValidationProblemDetails { Detail = "Body requerido." });

        try
        {
            var crt = await _svc.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById),
                new { loteId = crt.LoteId, repId = crt.ReproductoraId }, crt);
        }
        catch (InvalidOperationException ex)
        {
            // Errores de negocio: duplicado, Lote no pertenece al tenant, etc.
            return ValidationProblem(new ValidationProblemDetails { Detail = ex.Message });
        }
    }

    // ======================================
    // CREAR (bulk)
    // ======================================
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(IEnumerable<LoteReproductoraDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<LoteReproductoraDto>>> CreateBulk(
        [FromBody] IEnumerable<CreateLoteReproductoraDto> dtos,
        CancellationToken ct)
    {
        if (dtos is null)
            return ValidationProblem(new ValidationProblemDetails { Detail = "Body requerido." });

        try
        {
            var created = await _svc.CreateBulkAsync(dtos);
            // 201 para múltiples recursos (sin Location específico)
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(new ValidationProblemDetails { Detail = ex.Message });
        }
    }

    // ======================================
    // ACTUALIZAR
    // ======================================
    [HttpPut("{loteId}/{repId}")]
    [ProducesResponseType(typeof(LoteReproductoraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteReproductoraDto>> Update(
        [FromRoute] string loteId,
        [FromRoute] string repId,
        [FromBody] UpdateLoteReproductoraDto dto,
        CancellationToken ct)
    {
        if (dto is null)
            return ValidationProblem(new ValidationProblemDetails { Detail = "Body requerido." });

        if (!string.Equals(dto.LoteId, loteId, StringComparison.Ordinal) ||
            !string.Equals(dto.ReproductoraId, repId, StringComparison.Ordinal))
        {
            return ValidationProblem(new ValidationProblemDetails
            {
                Detail = "La ruta no coincide con el cuerpo (LoteId/ReproductoraId)."
            });
        }

        var upd = await _svc.UpdateAsync(dto);
        return upd is null ? NotFound() : Ok(upd);
    }

    // ======================================
    // ELIMINAR
    // ======================================
    [HttpDelete("{loteId}/{repId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] string loteId,
        [FromRoute] string repId,
        CancellationToken ct)
    {
        var ok = await _svc.DeleteAsync(loteId, repId);
        return ok ? NoContent() : NotFound();
    }

    // ======================================
    // OBTENER AVES DISPONIBLES DE UN LOTE
    // ======================================
    [HttpGet("{loteId}/aves-disponibles")]
    [ProducesResponseType(typeof(AvesDisponiblesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvesDisponiblesDto>> GetAvesDisponibles(
        [FromRoute] string loteId,
        CancellationToken ct)
    {
        var aves = await _svc.GetAvesDisponiblesAsync(loteId);
        return aves is null ? NotFound() : Ok(aves);
    }
}
