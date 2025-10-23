// src/ZooSanMarino.API/Controllers/FarmInventoryController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/farms/{farmId:int}/inventory")]
[Tags("Inventory")]
public class FarmInventoryController : ControllerBase
{
    private readonly IFarmInventoryService _service;
    private readonly IFarmInventoryReportService _report;

    public FarmInventoryController(
        IFarmInventoryService service,
        IFarmInventoryReportService report)
    {
        _service = service;
        _report  = report;
    }

    // ===== Inventario (stock) =====
    // GET /api/farms/{farmId}/inventory
    // GET /farms/{farmId}/inventory (alias sin /api)
    [HttpGet]
    [HttpGet("/farms/{farmId:int}/inventory")]
    [ProducesResponseType(typeof(IEnumerable<FarmInventoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(int farmId, [FromQuery] string? q = null, CancellationToken ct = default)
    {
        var items = await _service.GetByFarmAsync(farmId, q, ct);
        return Ok(items);
    }

    // GET /api/farms/{farmId}/inventory/{id}
    // GET /farms/{farmId}/inventory/{id}
    [HttpGet("{id:int}")]
    [HttpGet("/farms/{farmId:int}/inventory/{id:int}")]
    [ProducesResponseType(typeof(FarmInventoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int farmId, int id, CancellationToken ct = default)
    {
        var item = await _service.GetByIdAsync(farmId, id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    // POST /api/farms/{farmId}/inventory
    // POST /farms/{farmId}/inventory
    [HttpPost]
    [HttpPost("/farms/{farmId:int}/inventory")]
    [ProducesResponseType(typeof(FarmInventoryDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateOrReplace(int farmId, [FromBody] FarmInventoryCreateRequest req, CancellationToken ct = default)
    {
        var created = await _service.CreateOrReplaceAsync(farmId, req, ct);
        return CreatedAtAction(nameof(GetById), new { farmId, id = created.Id }, created);
    }

    // PUT /api/farms/{farmId}/inventory/{id}
    // PUT /farms/{farmId}/inventory/{id}
    [HttpPut("{id:int}")]
    [HttpPut("/farms/{farmId:int}/inventory/{id:int}")]
    [ProducesResponseType(typeof(FarmInventoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int farmId, int id, [FromBody] FarmInventoryUpdateRequest req, CancellationToken ct = default)
    {
        var updated = await _service.UpdateAsync(farmId, id, req, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    // DELETE /api/farms/{farmId}/inventory/{id}?hard=
    // DELETE /farms/{farmId}/inventory/{id}?hard=
    [HttpDelete("{id:int}")]
    [HttpDelete("/farms/{farmId:int}/inventory/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int farmId, int id, [FromQuery] bool hard = false, CancellationToken ct = default)
    {
        var ok = await _service.DeleteAsync(farmId, id, hard, ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Kardex =====
    // GET /api/farms/{farmId}/inventory/kardex?catalogItemId=&from=&to=
    // GET /farms/{farmId}/inventory/kardex?catalogItemId=&from=&to=
    [HttpGet("kardex")]
    [HttpGet("/farms/{farmId:int}/inventory/kardex")]
    [ProducesResponseType(typeof(IEnumerable<KardexItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetKardex(
        int farmId,
        [FromQuery] int catalogItemId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        if (catalogItemId <= 0) return BadRequest("catalogItemId es requerido.");
        var items = await _report.GetKardexAsync(farmId, catalogItemId, from, to, ct);
        return Ok(items);
    }

    // ===== Conteo físico =====
    // POST /api/farms/{farmId}/inventory/stock-count
    // POST /farms/{farmId}/inventory/stock-count
    [HttpPost("stock-count")]
    [HttpPost("/farms/{farmId:int}/inventory/stock-count")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PostStockCount(int farmId, [FromBody] StockCountRequest req, CancellationToken ct = default)
    {
        await _report.ApplyStockCountAsync(farmId, req, ct);
        return NoContent();
    }
}
