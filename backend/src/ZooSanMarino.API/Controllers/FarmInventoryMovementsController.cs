// src/ZooSanMarino.API/Controllers/FarmInventoryMovementsController.cs
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using CommonDtos = ZooSanMarino.Application.DTOs.Common;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/farms/{farmId:int}/inventory/movements")]
[Tags("Inventory Movements")]
public class FarmInventoryMovementsController : ControllerBase
{
    private readonly IFarmInventoryMovementService _service;

    public FarmInventoryMovementsController(IFarmInventoryMovementService service)
    {
        _service = service;
    }

    // DTO de respuesta para transfer (nombres JSON: out / In)
    public record TransferResponse(
        [property: JsonPropertyName("out")] InventoryMovementDto Out,
        [property: JsonPropertyName("In")]  InventoryMovementDto In
    );

    // ===== POST in =====
    // /api/farms/{farmId}/inventory/movements/in
    // /farms/{farmId}/inventory/movements/in
    [HttpPost("in")]
    [HttpPost("/farms/{farmId:int}/inventory/movements/in")]
    [ProducesResponseType(typeof(InventoryMovementDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> PostEntry(
        int farmId,
        [FromBody] InventoryEntryRequest req,
        CancellationToken ct = default)
    {
        var mov = await _service.PostEntryAsync(farmId, req, ct);
        return CreatedAtAction(nameof(GetById), new { farmId, movementId = mov.Id }, mov);
    }

    // ===== POST out =====
    [HttpPost("out")]
    [HttpPost("/farms/{farmId:int}/inventory/movements/out")]
    [ProducesResponseType(typeof(InventoryMovementDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> PostExit(
        int farmId,
        [FromBody] InventoryExitRequest req,
        CancellationToken ct = default)
    {
        var mov = await _service.PostExitAsync(farmId, req, ct);
        return CreatedAtAction(nameof(GetById), new { farmId, movementId = mov.Id }, mov);
    }

    // ===== POST transfer =====
    [HttpPost("transfer")]
    [HttpPost("/farms/{farmId:int}/inventory/movements/transfer")]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> PostTransfer(
        int farmId,
        [FromBody] InventoryTransferRequest req,
        CancellationToken ct = default)
    {
        var (outMov, inMov) = await _service.PostTransferAsync(farmId, req, ct);
        // Devolvemos { "out": ..., "In": ... } tal como espera el front
        return CreatedAtAction(nameof(GetById), new { farmId, movementId = outMov.Id }, new TransferResponse(outMov, inMov));
    }

    // ===== POST adjust (+/-) =====
    [HttpPost("adjust")]
    [HttpPost("/farms/{farmId:int}/inventory/movements/adjust")]
    [ProducesResponseType(typeof(InventoryMovementDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> PostAdjust(
        int farmId,
        [FromBody] InventoryAdjustRequest req,
        CancellationToken ct = default)
    {
        var mov = await _service.PostAdjustAsync(farmId, req, ct);
        return CreatedAtAction(nameof(GetById), new { farmId, movementId = mov.Id }, mov);
    }

    // ===== GET list (paginado + filtros) =====
    [HttpGet]
    [HttpGet("/farms/{farmId:int}/inventory/movements")]
    [ProducesResponseType(typeof(CommonDtos.PagedResult<InventoryMovementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        int farmId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? catalogItemId,
        [FromQuery] string? codigo,
        [FromQuery] string? type,   // Entry|Exit|TransferIn|TransferOut|Adjust
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new MovementQuery
        {
            From = from,
            To = to,
            CatalogItemId = catalogItemId,
            Codigo = codigo,
            Type = type,
            Page = page,
            PageSize = pageSize
        };

        var res = await _service.GetPagedAsync(farmId, query, ct);
        return Ok(res);
    }

    // ===== GET detalle =====
    [HttpGet("{movementId:int}")]
    [HttpGet("/farms/{farmId:int}/inventory/movements/{movementId:int}")]
    [ProducesResponseType(typeof(InventoryMovementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int farmId,
        int movementId,
        CancellationToken ct = default)
    {
        var mov = await _service.GetByIdAsync(farmId, movementId, ct);
        return mov is null ? NotFound() : Ok(mov);
    }
}
