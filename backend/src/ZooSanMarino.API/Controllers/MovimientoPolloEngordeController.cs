// src/ZooSanMarino.API/Controllers/MovimientoPolloEngordeController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Tags("Movimiento de Pollo Engorde")]
public class MovimientoPolloEngordeController : ControllerBase
{
    private readonly IMovimientoPolloEngordeService _service;
    private readonly IMovimientoPolloEngordeFilterDataService _filterDataService;

    public MovimientoPolloEngordeController(
        IMovimientoPolloEngordeService service,
        IMovimientoPolloEngordeFilterDataService filterDataService)
    {
        _service = service;
        _filterDataService = filterDataService;
    }

    /// <summary>
    /// Granjas asignadas al usuario, núcleos, galpones y lotes (Ave Engorde + Reproductora) en una sola respuesta para filtros en cascada.
    /// </summary>
    [HttpGet("filter-data")]
    [ProducesResponseType(typeof(MovimientoPolloEngordeFilterDataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFilterData(CancellationToken ct)
    {
        var data = await _filterDataService.GetFilterDataAsync(ct);
        return Ok(data);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MovimientoPolloEngordeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var items = await _service.GetAllAsync();
        return Ok(items);
    }

    [HttpPost("search")]
    [ProducesResponseType(typeof(ZooSanMarino.Application.DTOs.Common.PagedResult<MovimientoPolloEngordeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] MovimientoPolloEngordeSearchRequest request)
    {
        var result = await _service.SearchAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Resumen de aves del lote para reportes: aves con que inició, cuántas salieron (completados), cuántas vendidas, aves actuales.
    /// Debe estar antes de {id} para que la ruta literal coincida.
    /// </summary>
    [HttpGet("resumen-aves-lote")]
    [ProducesResponseType(typeof(ResumenAvesLoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResumenAvesLote([FromQuery] string tipoLote, [FromQuery] int loteId)
    {
        var resumen = await _service.GetResumenAvesLoteAsync(tipoLote, loteId);
        if (resumen == null) return NotFound();
        return Ok(resumen);
    }

    /// <summary>Resúmenes de varios lotes en una sola petición (una fila por id solicitado).</summary>
    [HttpPost("resumen-aves-lotes")]
    [ProducesResponseType(typeof(ResumenAvesLotesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostResumenAvesLotes([FromBody] ResumenAvesLotesRequest request)
    {
        if (request is null) return BadRequest(new { error = "Body requerido." });
        try
        {
            var res = await _service.GetResumenAvesLotesAsync(request);
            return Ok(res);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Disponibilidad por lote para ventas (incluye reservas en estado Pendiente para evitar sobreventa).
    /// </summary>
    [HttpPost("aves-disponibles-lotes")]
    [ProducesResponseType(typeof(AvesDisponiblesLotesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostAvesDisponiblesLotes([FromBody] AvesDisponiblesLotesRequest request)
    {
        if (request is null) return BadRequest(new { error = "Body requerido." });
        try
        {
            var res = await _service.GetAvesDisponiblesLotesAsync(request);
            return Ok(res);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Auditoría de coherencia (ventas vs disponibilidad) por granja/lotes y corrección opcional (solo Pendiente).
    /// </summary>
    [HttpPost("auditar-ventas")]
    [ProducesResponseType(typeof(AuditoriaVentasEngordeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostAuditarVentas([FromBody] AuditoriaVentasEngordeRequest request)
    {
        if (request is null) return BadRequest(new { error = "Body requerido." });
        try
        {
            var res = await _service.AuditarVentasEngordeAsync(request);
            return Ok(res);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Corrige incoherencias de ventas en estado Completado ajustando cantidades (devuelve al lote solo lo necesario).
    /// </summary>
    [HttpPost("corregir-ventas-completadas")]
    [ProducesResponseType(typeof(CorregirVentasCompletadasResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostCorregirVentasCompletadas([FromBody] CorregirVentasCompletadasRequest request)
    {
        if (request is null) return BadRequest(new { error = "Body requerido." });
        try
        {
            var res = await _service.CorregirVentasCompletadasAsync(request);
            return Ok(res);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, message = ex.Message });
        }
    }

    /// <summary>Venta por granja: varios movimientos Pendiente con la misma cabecera de despacho, en una transacción.</summary>
    [HttpPost("venta-granja-despacho")]
    [ProducesResponseType(typeof(VentaGranjaDespachoResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostVentaGranjaDespacho([FromBody] CreateVentaGranjaDespachoDto dto)
    {
        if (dto is null) return BadRequest(new { error = "Body requerido." });
        try
        {
            var res = await _service.CreateVentaGranjaDespachoAsync(dto);
            return StatusCode(StatusCodes.Status201Created, res);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Ventas (Venta/Despacho/Retiro) de un lote Ave Engorde con información completa de peso
    /// (pesoNeto individual, pesoBrutoGlobal, pesoTaraGlobal, pesoNetoGlobal, promedioPesoAve).
    /// Ordenadas por fecha de movimiento ascendente.
    /// </summary>
    [HttpGet("por-lote/{loteId:int}/ventas-con-peso")]
    [ProducesResponseType(typeof(IEnumerable<MovimientoPolloEngordeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetVentasConPeso(int loteId)
    {
        if (loteId <= 0) return BadRequest(new { error = "loteId inválido." });
        var result = await _service.SearchAsync(new MovimientoPolloEngordeSearchRequest(
            LoteAveEngordeOrigenId: loteId,
            Page: 1,
            PageSize: 1000,
            SortBy: "FechaMovimiento",
            SortDesc: false
        ));
        // Filtramos solo movimientos de tipo venta/salida que no estén anulados/cancelados.
        var ventas = (result.Items ?? Enumerable.Empty<MovimientoPolloEngordeDto>())
            .Where(m =>
                (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro")
                && m.Estado != "Cancelado" && m.Estado != "Anulado")
            .ToList();
        return Ok(ventas);
    }

    /// <summary>
    /// Organiza (o simula) la corrección masiva de peso prorrateado en ventas históricas.
    /// Agrupa ventas por NumeroDespacho y recalcula PesoNeto / PromedioPesoAve proporcional a las aves de cada movimiento.
    /// Use DryRun=true (default) para previsualizar sin guardar cambios.
    /// </summary>
    [HttpPost("organizar-peso")]
    [ProducesResponseType(typeof(OrganizarPesoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostOrganizarPeso([FromBody] OrganizarPesoRequest request)
    {
        if (request is null) return BadRequest(new { error = "Body requerido." });
        try
        {
            var res = await _service.OrganizarPesoAsync(request);
            return Ok(res);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Completa varios movimientos Pendiente en una transacción.</summary>
    [HttpPost("completar-batch")]
    [ProducesResponseType(typeof(IReadOnlyList<MovimientoPolloEngordeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostCompletarBatch([FromBody] CompletarMovimientosBatchRequest request)
    {
        if (request is null) return BadRequest(new { error = "Body requerido." });
        try
        {
            var res = await _service.CompletarBatchAsync(request.MovimientoIds);
            return Ok(res);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MovimientoPolloEngordeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _service.GetByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MovimientoPolloEngordeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateMovimientoPolloEngordeDto dto)
    {
        try
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(MovimientoPolloEngordeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMovimientoPolloEngordeDto dto)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Elimina el movimiento (soft-delete). Si estaba completado, devuelve las aves al lote de origen y ajusta el destino si había traslado.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Eliminar(int id, [FromQuery] string? motivo = null)
    {
        try
        {
            var ok = await _service.EliminarAsync(id, motivo);
            if (!ok) return NotFound();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, message = ex.Message });
        }
    }

    [HttpPost("{id:int}/cancelar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancelar(int id, [FromBody] CancelarMovimientoPolloEngordeRequest request)
    {
        try
        {
            var ok = await _service.CancelAsync(id, request.Motivo ?? "");
            if (!ok) return NotFound();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Completa el movimiento: descuenta aves del lote origen y suma al destino (si existe). El movimiento pasa a estado Completado.
    /// </summary>
    [HttpPost("{id:int}/completar")]
    [ProducesResponseType(typeof(MovimientoPolloEngordeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Completar(int id)
    {
        try
        {
            var result = await _service.CompleteAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

}

public record CancelarMovimientoPolloEngordeRequest(string? Motivo);
