// src/ZooSanMarino.API/Controllers/InventarioGestionController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.Calculos;
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

    /// <summary>
    /// Ventana de fechas de los movimientos cargados A MANO: del 1 del mes en curso hasta hoy
    /// (<see cref="VentanaFechaMovimientoInventarioCalculos"/>). Devuelve el 400 ya armado, o
    /// <c>null</c> si la fecha es válida.
    /// <para>
    /// ⚠️ La guarda vive acá y NO en <c>InventarioGestionService</c> a propósito: los mismos métodos
    /// del servicio los llaman la carga masiva, las devoluciones de alimento al editar o borrar un
    /// seguimiento diario y la anulación de gastos, que escriben con fecha histórica legítimamente.
    /// El controller es la única frontera «esto lo tipeó una persona en pantalla».
    /// </para>
    /// </summary>
    private IActionResult? ValidarVentanaFecha(DateTime? fecha)
    {
        var hoy = VentanaFechaMovimientoInventarioCalculos.DiaOperativo(DateTimeOffset.UtcNow);
        return VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(fecha, hoy)
            ? null
            : BadRequest(new { message = VentanaFechaMovimientoInventarioCalculos.MensajeFueraDeVentana(hoy) });
    }

    /// <summary>
    /// D4 — misma ventana que <see cref="ValidarVentanaFecha"/> más la excepción del alimento previo
    /// al encasetamiento: una fecha del mes anterior se admite si cae dentro de los
    /// <c>dias_alimento_previo_encaset</c> días previos a un encasetamiento REAL de ese galpón.
    /// <para>
    /// Se aplica SOLO a las dos puertas de ingreso —que son las del alimento que llega antes que los
    /// pollitos—. Las otras tres puertas (traslado, fecha de traslado y stock) conservan la regla dura.
    /// El futuro y los más de 30 días hacia atrás siguen prohibidos por las dos vías.
    /// </para>
    /// </summary>
    private async Task<IActionResult?> ValidarVentanaFechaIngresoAsync(
        DateTime? fecha,
        Func<CancellationToken, Task<InventarioGestionVentanaAlimentoPrevioDto>> resolverVentana,
        CancellationToken ct)
    {
        var hoy = VentanaFechaMovimientoInventarioCalculos.DiaOperativo(DateTimeOffset.UtcNow);
        if (VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(fecha, hoy))
            return null;

        var ventana = await resolverVentana(ct);
        if (VentanaFechaMovimientoInventarioCalculos.EsFechaPermitidaConEncasetProximo(
                fecha, hoy, ventana.ProximoEncaset, ventana.DiasVentanaEmpresa))
            return null;

        return BadRequest(new
        {
            message = VentanaFechaMovimientoInventarioCalculos.MensajeFueraDeVentanaConEncaset(
                hoy, ventana.ProximoEncaset, ventana.DiasVentanaEmpresa)
        });
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
        if (ValidarVentanaFecha(req.FechaIngreso) is { } fueraDeVentana) return fueraDeVentana;
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
        var fueraDeVentana = await ValidarVentanaFechaIngresoAsync(
            req.FechaMovimiento,
            c => _service.ResolverVentanaAlimentoPrevioEncasetAsync(
                req.FarmId, req.NucleoId, req.GalponId, req.FechaMovimiento ?? DateTime.UtcNow, c),
            ct);
        if (fueraDeVentana is not null) return fueraDeVentana;
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
        if (ValidarVentanaFecha(req.FechaMovimiento) is { } fueraDeVentana) return fueraDeVentana;
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
        [FromQuery] string? tipoOperacion = null,
        [FromQuery] string? unit = null,
        [FromQuery] string? referenceContains = null,
        [FromQuery] string? reasonContains = null,
        [FromQuery] string? transferGroupId = null,
        [FromQuery] int? itemInventarioEcuadorId = null,
        [FromQuery] int? fromFarmId = null,
        [FromQuery] string? fromNucleoId = null,
        [FromQuery] string? fromGalponId = null,
        CancellationToken ct = default)
    {
        var list = await _service.GetMovimientosAsync(
            farmId, fechaDesde, fechaHasta, estado, movementType, nucleoId, galponId, loteId, search, concepto, tipoItem,
            tipoOperacion, unit, referenceContains, reasonContains, transferGroupId, itemInventarioEcuadorId,
            fromFarmId, fromNucleoId, fromGalponId, ct);
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
            var resultado = await _service.RegistrarRecepcionTransitoAsync(req, ct);
            // Respuesta aditiva: destino/movimiento (primera ubicación) conservan el contrato previo;
            // destinos/movimientos traen todas las ubicaciones cuando la recepción se distribuye.
            return Ok(new
            {
                destino = resultado.Destinos.FirstOrDefault(),
                movimiento = resultado.Movimientos.FirstOrDefault(),
                destinos = resultado.Destinos,
                movimientos = resultado.Movimientos
            });
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

    // ─── TRASLADOS ───────────────────────────────────────────────────────────

    /// <summary>Lista de traslados agrupados por TransferGroupId. Filtros opcionales: granja, núcleo, galpón, rango de fechas, búsqueda de ítem, tipo de ítem.</summary>
    [HttpGet("traslados")]
    [ProducesResponseType(typeof(IEnumerable<InventarioGestionTrasladoListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTraslados(
        [FromQuery] int? farmId = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] string? search = null,
        [FromQuery] string? itemTipoItem = null,
        [FromQuery] string? nucleoId = null,
        [FromQuery] string? galponId = null,
        CancellationToken ct = default)
    {
        var list = await _service.GetTrasladosAsync(farmId, fechaDesde, fechaHasta, search, itemTipoItem, nucleoId, galponId, ct);
        return Ok(list);
    }

    /// <summary>Actualiza la fecha de movimiento de un traslado (aplica a todos los registros del grupo).</summary>
    [HttpPut("traslados/{transferGroupId:guid}/fecha")]
    [ProducesResponseType(typeof(InventarioGestionTrasladoListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarFechaTraslado(Guid transferGroupId, [FromBody] InventarioGestionActualizarFechaTrasladoRequest req, CancellationToken ct = default)
    {
        if (ValidarVentanaFecha(req.FechaMovimiento) is { } fueraDeVentana) return fueraDeVentana;
        try
        {
            var result = await _service.ActualizarFechaTrasladoAsync(transferGroupId, req, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Elimina un traslado completo: revierte stock en origen/destino y marca
    /// anulado=true en lote_registro_historico_unificado para todos los movimientos del grupo.
    /// </summary>
    [HttpDelete("traslados/{transferGroupId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EliminarTraslado(Guid transferGroupId, CancellationToken ct = default)
    {
        try
        {
            await _service.EliminarTrasladoAsync(transferGroupId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── INGRESOS ────────────────────────────────────────────────────────────

    /// <summary>Lista de ingresos (directos y de traslados). Filtros opcionales: granja, núcleo, galpón, rango de fechas, búsqueda de ítem, tipo de ítem.</summary>
    [HttpGet("ingresos")]
    [ProducesResponseType(typeof(IEnumerable<InventarioGestionIngresoListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIngresos(
        [FromQuery] int? farmId = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] string? search = null,
        [FromQuery] string? itemTipoItem = null,
        [FromQuery] string? nucleoId = null,
        [FromQuery] string? galponId = null,
        CancellationToken ct = default)
    {
        var list = await _service.GetIngresosAsync(farmId, fechaDesde, fechaHasta, search, itemTipoItem, nucleoId, galponId, ct);
        return Ok(list);
    }

    /// <summary>Actualiza la fecha de movimiento de un ingreso.</summary>
    [HttpPut("ingresos/{movimientoId:int}/fecha")]
    [ProducesResponseType(typeof(InventarioGestionIngresoListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarFechaIngreso(int movimientoId, [FromBody] InventarioGestionActualizarFechaIngresoRequest req, CancellationToken ct = default)
    {
        // La ubicación no viaja en el request: sale del movimiento que se está editando.
        var fueraDeVentana = await ValidarVentanaFechaIngresoAsync(
            req.FechaMovimiento,
            c => _service.ResolverVentanaAlimentoPrevioEncasetDeIngresoAsync(movimientoId, req.FechaMovimiento, c),
            ct);
        if (fueraDeVentana is not null) return fueraDeVentana;
        try
        {
            var result = await _service.ActualizarFechaIngresoAsync(movimientoId, req, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Marca (o desmarca) un ingreso como «alimento para el PRÓXIMO encasetamiento de este galpón».
    /// <para>
    /// Es la atribución EXPLÍCITA al ciclo siguiente para los galpones encadenados, donde la fecha
    /// sola no alcanza: la llegada real 2-7 días antes del encaset cae dentro del ciclo anterior y el
    /// corte por fecha la descartaría. Sincroniza el espejo <c>lote_registro_historico_unificado</c>.
    /// </para>
    /// </summary>
    [HttpPut("ingresos/{movimientoId:int}/destino-ciclo")]
    [ProducesResponseType(typeof(InventarioGestionIngresoListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarDestinoCicloIngreso(int movimientoId, [FromBody] InventarioGestionActualizarDestinoCicloRequest req, CancellationToken ct = default)
    {
        try
        {
            var result = await _service.ActualizarDestinoCicloIngresoAsync(movimientoId, req, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Elimina un ingreso (Ingreso / TrasladoEntrada / TrasladoInterGranjaEntrada): revierte stock
    /// y marca anulado=true en lote_registro_historico_unificado.
    /// </summary>
    [HttpDelete("ingresos/{movimientoId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EliminarIngreso(int movimientoId, CancellationToken ct = default)
    {
        try
        {
            await _service.EliminarIngresoAsync(movimientoId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
