// file: src/ZooSanMarino.API/Controllers/LoteReproductoraController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LoteReproductoraController : ControllerBase
{
    private readonly ILoteReproductoraService _svc;
    public LoteReproductoraController(ILoteReproductoraService svc) => _svc = svc;

    // ======================================
    // LISTADO (opcionalmente filtrado por lote)
    // GET /api/LoteReproductora?loteId=L001
    // ======================================
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LoteReproductoraDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LoteReproductoraDto>>> GetAll(
        [FromQuery] int? loteId,  // Changed from string? to int?
        CancellationToken ct)
    {
        // Nota: el service actual no acepta ct; si lo agregas en futuro, pásalo.
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
        [FromRoute] int loteId,  // Changed from string to int
        [FromRoute] string repId,
        CancellationToken ct)
    {
        var dto = await _svc.GetByIdAsync(loteId, repId);  // Changed from loteId
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
        [FromRoute] int loteId,  // Changed from string to int
        [FromRoute] string repId,
        [FromBody] UpdateLoteReproductoraDto dto,
        CancellationToken ct)
    {
        if (dto is null)
            return ValidationProblem(new ValidationProblemDetails { Detail = "Body requerido." });

        if (dto.LoteId != loteId ||  // Changed from string.Equals to int comparison
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
        [FromRoute] int loteId,  // Changed from string to int
        [FromRoute] string repId,
        CancellationToken ct)
    {
        var ok = await _svc.DeleteAsync(loteId, repId);  // Changed from loteId
        return ok ? NoContent() : NotFound();
    }
}
