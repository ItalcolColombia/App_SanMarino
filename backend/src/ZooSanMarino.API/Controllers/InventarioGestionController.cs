// src/ZooSanMarino.API/Controllers/InventarioGestionController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/inventario-gestion")]
[Tags("Inventario Gestion")]
public class InventarioGestionController : ControllerBase
{
    private readonly IInventarioGestionService _service;

    public InventarioGestionController(IInventarioGestionService service)
    {
        _service = service;
    }

    /// <summary>Datos para filtros: Granja → Núcleo → Galpón (usado en Panama/Ecuador).</summary>
    [HttpGet("filter-data")]
    [ProducesResponseType(typeof(InventarioGestionFilterDataDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFilterData(CancellationToken ct = default)
    {
        var data = await _service.GetFilterDataAsync(ct);
        return Ok(data);
    }

    /// <summary>Stock con filtros opcionales por granja, núcleo, galpón, tipo de ítem.</summary>
    [HttpGet("stock")]
    [ProducesResponseType(typeof(IEnumerable<InventarioGestionStockDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStock(
        [FromQuery] int? farmId = null,
        [FromQuery] string? nucleoId = null,
        [FromQuery] string? galponId = null,
        [FromQuery] string? itemType = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var list = await _service.GetStockAsync(farmId, nucleoId, galponId, itemType, search, ct);
        return Ok(list);
    }

    /// <summary>Registra un ingreso. Alimento: obligatorio Granja+Núcleo+Galpón; otros: solo Granja.</summary>
    [HttpPost("ingreso")]
    [ProducesResponseType(typeof(InventarioGestionStockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegistrarIngreso([FromBody] InventarioGestionIngresoRequest req, CancellationToken ct = default)
    {
        try
        {
            var result = await _service.RegistrarIngresoAsync(req, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Registra un traslado entre ubicaciones. Alimento: entre galpones; otros: entre granjas.</summary>
    [HttpPost("traslado")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegistrarTraslado([FromBody] InventarioGestionTrasladoRequest req, CancellationToken ct = default)
    {
        try
        {
            var (origen, destino) = await _service.RegistrarTrasladoAsync(req, ct);
            return Ok(new { origen, destino });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Registra consumo (reduce stock). Usado desde Seguimiento Diario. Para devolución usar ingreso.</summary>
    [HttpPost("consumo")]
    [ProducesResponseType(typeof(InventarioGestionStockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegistrarConsumo([FromBody] InventarioGestionConsumoRequest req, CancellationToken ct = default)
    {
        try
        {
            var result = await _service.RegistrarConsumoAsync(req, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Histórico de movimientos (entradas, salidas, traslados) con filtros opcionales.</summary>
    [HttpGet("movimientos")]
    [ProducesResponseType(typeof(IEnumerable<InventarioGestionMovimientoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovimientos(
        [FromQuery] int? farmId = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] string? estado = null,
        [FromQuery] string? movementType = null,
        CancellationToken ct = default)
    {
        var list = await _service.GetMovimientosAsync(farmId, fechaDesde, fechaHasta, estado, movementType, ct);
        return Ok(list);
    }
}
