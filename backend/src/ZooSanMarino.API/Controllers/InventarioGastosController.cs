using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/inventario-gastos")]
[Authorize]
[Tags("Inventario Gastos")]
public class InventarioGastosController : ControllerBase
{
    private readonly IInventarioGastoService _svc;

    public InventarioGastosController(IInventarioGastoService svc)
    {
        _svc = svc;
    }

    [HttpGet("filter-data")]
    [ProducesResponseType(typeof(LoteReproductoraFilterDataDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFilterData(CancellationToken ct = default)
    {
        var data = await _svc.GetFilterDataAsync(ct);
        return Ok(data);
    }

    [HttpGet("conceptos")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConceptos(CancellationToken ct = default)
    {
        var list = await _svc.GetConceptosAsync(ct);
        return Ok(list);
    }

    [HttpGet("items")]
    [ProducesResponseType(typeof(IEnumerable<InventarioGastoItemStockDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetItems([FromQuery] int farmId, [FromQuery] string concepto, CancellationToken ct = default)
    {
        try
        {
            var list = await _svc.GetItemsWithStockAsync(farmId, concepto, ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<InventarioGastoListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] int? farmId = null,
        [FromQuery] string? nucleoId = null,
        [FromQuery] string? galponId = null,
        [FromQuery] int? loteAveEngordeId = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] string? concepto = null,
        [FromQuery] string? search = null,
        [FromQuery] string? estado = null,
        CancellationToken ct = default)
    {
        var req = new InventarioGastoSearchRequest(
            FarmId: farmId,
            NucleoId: nucleoId,
            GalponId: galponId,
            LoteAveEngordeId: loteAveEngordeId,
            FechaDesde: fechaDesde,
            FechaHasta: fechaHasta,
            Concepto: concepto,
            Search: search,
            Estado: estado
        );
        var list = await _svc.SearchAsync(req, ct);
        return Ok(list);
    }

    /// <summary>Exportación detallada (una fila por línea de consumo), con nombres de granja, núcleo y galpón.</summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(IEnumerable<InventarioGastoExportRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(
        [FromQuery] int? farmId = null,
        [FromQuery] string? nucleoId = null,
        [FromQuery] string? galponId = null,
        [FromQuery] int? loteAveEngordeId = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] string? concepto = null,
        [FromQuery] string? search = null,
        [FromQuery] string? estado = null,
        CancellationToken ct = default)
    {
        var req = new InventarioGastoSearchRequest(
            FarmId: farmId,
            NucleoId: nucleoId,
            GalponId: galponId,
            LoteAveEngordeId: loteAveEngordeId,
            FechaDesde: fechaDesde,
            FechaHasta: fechaHasta,
            Concepto: concepto,
            Search: search,
            Estado: estado
        );
        var rows = await _svc.ExportAsync(req, ct);
        return Ok(rows);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InventarioGastoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        try
        {
            var dto = await _svc.GetByIdAsync(id, ct);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(InventarioGastoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateInventarioGastoRequest req, CancellationToken ct = default)
    {
        try
        {
            var dto = await _svc.CreateAsync(req, ct);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id, [FromQuery] string? motivo = null, CancellationToken ct = default)
    {
        try
        {
            await _svc.DeleteAsync(id, motivo, ct);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

