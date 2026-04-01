using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Application.DTOs;
using CommonDtos = ZooSanMarino.Application.DTOs.Common;
using LoteAveEngordeDtos = ZooSanMarino.Application.DTOs.LoteAveEngorde;
using ZooSanMarino.Application.DTOs.Lotes;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Consumes("application/json")]
public class LoteAveEngordeController : ControllerBase
{
    private readonly ILoteAveEngordeService _svc;
    private readonly IServiceScopeFactory _scopeFactory;

    public LoteAveEngordeController(ILoteAveEngordeService svc, IServiceScopeFactory scopeFactory)
    {
        _svc = svc;
        _scopeFactory = scopeFactory;
    }

    // ===========================
    // LISTADO SIMPLE CON INFORMACIÓN COMPLETA DE RELACIONES
    // ===========================
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LoteAveEngordeDtos.LoteAveEngordeDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LoteAveEngordeDtos.LoteAveEngordeDetailDto>>> GetAll()
    {
        var items = await _svc.GetAllAsync();
        return Ok(items);
    }

    // ===========================
    // BÚSQUEDA AVANZADA (paginada)
    // ===========================
    [HttpGet("search")]
    [ProducesResponseType(typeof(CommonDtos.PagedResult<LoteAveEngordeDtos.LoteAveEngordeDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CommonDtos.PagedResult<LoteAveEngordeDtos.LoteAveEngordeDetailDto>>> Search([FromQuery] LoteAveEngordeDtos.LoteAveEngordeSearchRequest req)
    {
        var res = await _svc.SearchAsync(req);
        return Ok(res);
    }

    /// <summary>
    /// Datos para el modal de crear/editar lote de engorde (granjas, núcleos, galpones, técnicos, compañías, razas) en una sola llamada.
    /// Reutiliza el mismo form-data que Lote.
    /// </summary>
    [HttpGet("form-data")]
    [ProducesResponseType(typeof(LoteFormDataDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoteFormDataDto>> GetFormData(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var formDataSvc = scope.ServiceProvider.GetRequiredService<ILoteFormDataService>();
        var data = await formDataSvc.GetFormDataAsync(ct);
        return Ok(data);
    }

    // ===========================
    // DETALLE
    // ===========================
    [HttpGet("{loteAveEngordeId}")]
    [ProducesResponseType(typeof(LoteAveEngordeDtos.LoteAveEngordeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteAveEngordeDtos.LoteAveEngordeDetailDto>> GetById(int loteAveEngordeId)
    {
        var res = await _svc.GetByIdAsync(loteAveEngordeId);
        if (res is null) return NotFound();
        return Ok(res);
    }

    // ===========================
    // CREATE
    // ===========================
    [HttpPost]
    [ProducesResponseType(typeof(LoteAveEngordeDtos.LoteAveEngordeDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoteAveEngordeDtos.LoteAveEngordeDetailDto>> Create([FromBody] CreateLoteAveEngordeDto dto)
    {
        if (dto is null) return BadRequest("Body requerido.");
        try
        {
            var created = await _svc.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { loteAveEngordeId = created.LoteAveEngordeId }, created);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    // ===========================
    // UPDATE
    // ===========================
    [HttpPut("{loteAveEngordeId}")]
    [ProducesResponseType(typeof(LoteAveEngordeDtos.LoteAveEngordeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteAveEngordeDtos.LoteAveEngordeDetailDto>> Update(int loteAveEngordeId, [FromBody] UpdateLoteAveEngordeDto dto)
    {
        if (dto is null) return BadRequest("Body requerido.");
        if (dto.LoteAveEngordeId <= 0) return BadRequest("LoteAveEngordeId debe ser mayor que 0.");
        if (loteAveEngordeId != dto.LoteAveEngordeId)
            return BadRequest("El id de la ruta no coincide con el del cuerpo.");

        try
        {
            var updated = await _svc.UpdateAsync(dto);
            if (updated is null) return NotFound();
            return Ok(updated);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    // ===========================
    // DELETE (soft)
    // ===========================
    [HttpDelete("{loteAveEngordeId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int loteAveEngordeId)
    {
        var ok = await _svc.DeleteAsync(loteAveEngordeId);
        return ok ? NoContent() : NotFound();
    }

    // ===========================
    // DELETE (hard)
    // ===========================
    [HttpDelete("{loteAveEngordeId}/hard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HardDelete(int loteAveEngordeId)
    {
        var ok = await _svc.HardDeleteAsync(loteAveEngordeId);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Cierra el lote operativamente (liquidación); guarda usuario que ejecuta la acción.</summary>
    [HttpPost("{loteAveEngordeId}/cerrar")]
    [ProducesResponseType(typeof(LoteAveEngordeDtos.LoteAveEngordeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteAveEngordeDtos.LoteAveEngordeDetailDto>> Cerrar(int loteAveEngordeId, [FromBody] CerrarLoteAveEngordeRequest? body)
    {
        if (body is null) return BadRequest("Body requerido.");
        try
        {
            var res = await _svc.CerrarLoteAsync(loteAveEngordeId, body);
            return res is null ? NotFound() : Ok(res);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Reabre un lote cerrado (motivo obligatorio).</summary>
    [HttpPost("{loteAveEngordeId}/abrir")]
    [ProducesResponseType(typeof(LoteAveEngordeDtos.LoteAveEngordeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteAveEngordeDtos.LoteAveEngordeDetailDto>> Abrir(int loteAveEngordeId, [FromBody] AbrirLoteAveEngordeRequest? body)
    {
        if (body is null) return BadRequest("Body requerido.");
        try
        {
            var res = await _svc.AbrirLoteAsync(loteAveEngordeId, body);
            return res is null ? NotFound() : Ok(res);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
