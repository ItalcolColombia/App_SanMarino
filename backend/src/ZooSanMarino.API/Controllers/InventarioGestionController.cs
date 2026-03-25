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

    /// <summary>Lotes en granjas asignadas y valores distintos de concepto, tipo de ítem y estado en el histórico (misma empresa / país).</summary>
    [HttpGet("historico-filtros")]
    [ProducesResponseType(typeof(InventarioGestionHistoricoFiltrosDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoricoFiltros(CancellationToken ct = default)
    {
        var data = await _service.GetHistoricoFiltrosAsync(ct);
        return Ok(data);
    }

    /// <summary>Actualiza cantidad/unidad de un registro de stock (ajuste manual). Mismas reglas de acceso que GET stock.</summary>
    [HttpPut("stock/{stockId:int}")]
    [ProducesResponseType(typeof(InventarioGestionStockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarStock(int stockId, [FromBody] InventarioGestionStockUpdateRequest req, CancellationToken ct = default)
    {
        try
        {
            var result = await _service.ActualizarStockAsync(stockId, req, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Elimina un registro de stock. Si había cantidad, se registra movimiento de salida.</summary>
    [HttpDelete("stock/{stockId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EliminarStock(int stockId, CancellationToken ct = default)
    {
        try
        {
            await _service.EliminarStockAsync(stockId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Stock solo en granjas asignadas al usuario; filtros opcionales: granja, núcleo, galpón, concepto/tipo ítem, búsqueda código/nombre.</summary>
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
        [FromQuery] string? nucleoId = null,
        [FromQuery] string? galponId = null,
        [FromQuery] int? loteId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? concepto = null,
        [FromQuery] string? tipoItem = null,
        CancellationToken ct = default)
    {
        var list = await _service.GetMovimientosAsync(farmId, fechaDesde, fechaHasta, estado, movementType, nucleoId, galponId, loteId, search, concepto, tipoItem, ct);
        return Ok(list);
    }

    /// <summary>Traslados inter-granja pendientes de recepción (inventario en tránsito). Opcional: granja destino.</summary>
    [HttpGet("transito/pendientes")]
    [ProducesResponseType(typeof(IEnumerable<InventarioGestionTransitoPendienteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransitosPendientes([FromQuery] int? farmIdDestino = null, CancellationToken ct = default)
    {
        var list = await _service.GetTransitosPendientesAsync(farmIdDestino, ct);
        return Ok(list);
    }

    /// <summary>Recepción en granja destino de un traslado inter-granja (cierra el tránsito).</summary>
    [HttpPost("transito/recepcion")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegistrarRecepcionTransito([FromBody] InventarioGestionRecepcionTransitoRequest req, CancellationToken ct = default)
    {
        try
        {
            var (destino, movimiento) = await _service.RegistrarRecepcionTransitoAsync(req, ct);
            return Ok(new { destino, movimiento });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Rechaza una solicitud inter-granja pendiente; no descuenta stock en origen.</summary>
    [HttpPost("transito/rechazo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RechazarTransito([FromBody] InventarioGestionRechazoTransitoRequest req, CancellationToken ct = default)
    {
        try
        {
            await _service.RechazarTransitoPendienteAsync(req, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Anula un registro del histórico (solo Consumo o Ingreso): revierte stock y elimina la fila del movimiento.
    /// </summary>
    [HttpDelete("movimientos/{movimientoId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AnularMovimientoHistorico(int movimientoId, [FromQuery] string? motivo = null, CancellationToken ct = default)
    {
        try
        {
            await _service.AnularMovimientoHistoricoAsync(movimientoId, motivo, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
