// API para Seguimiento Diario Aves de Engorde (seguimiento_diario tipo = 'engorde'). Misma forma que SeguimientoLoteLevante.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class SeguimientoAvesEngordeController : ControllerBase
{
    private readonly ISeguimientoAvesEngordeService _svc;
    private readonly ISeguimientoAvesEngordeFilterDataService _filterDataSvc;

    public SeguimientoAvesEngordeController(
        ISeguimientoAvesEngordeService svc,
        ISeguimientoAvesEngordeFilterDataService filterDataSvc)
    {
        _svc = svc;
        _filterDataSvc = filterDataSvc;
    }

    /// <summary>Datos para filtros en cascada (Granja → Núcleo → Galpón → Lote). Lotes = lote_ave_engorde (no lotes levante).</summary>
    [HttpGet("filter-data")]
    [ProducesResponseType(typeof(LoteReproductoraFilterDataDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoteReproductoraFilterDataDto>> GetFilterData(CancellationToken ct = default)
    {
        var data = await _filterDataSvc.GetFilterDataAsync(ct);
        return Ok(data);
    }

    /// <summary>
    /// Registros diarios del lote + historial unificado (inventario y ventas), orden cronológico.
    /// Misma información que GET por-lote/{id}/historico-unificado, incluida aquí para una sola petición.
    /// </summary>
    [HttpGet("por-lote/{loteId}")]
    [ProducesResponseType(typeof(SeguimientoAvesEngordePorLoteResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SeguimientoAvesEngordePorLoteResponseDto>> GetByLote(int loteId)
    {
        var data = await _svc.GetByLoteAsync(loteId);
        return Ok(data);
    }

    /// <summary>Historial unificado (inventario EC + ventas aves) del lote, orden por fecha de operación.</summary>
    [HttpGet("por-lote/{loteId}/historico-unificado")]
    [ProducesResponseType(typeof(IEnumerable<LoteRegistroHistoricoUnificadoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LoteRegistroHistoricoUnificadoDto>>> GetHistoricoUnificadoPorLote(int loteId)
    {
        var items = await _svc.GetHistoricoUnificadoPorLoteAsync(loteId);
        return Ok(items);
    }

    /// <summary>Resumen para liquidar lote: aves al inicio, ventas acumuladas, saldo alimento (kg).</summary>
    [HttpGet("por-lote/{loteId}/resumen-liquidacion")]
    [ProducesResponseType(typeof(LiquidacionLoteEngordeResumenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LiquidacionLoteEngordeResumenDto>> GetResumenLiquidacion(int loteId)
    {
        var res = await _svc.GetLiquidacionResumenAsync(loteId);
        return res is null ? NotFound() : Ok(res);
    }

    /// <summary>Obtener un registro por ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SeguimientoLoteLevanteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeguimientoLoteLevanteDto>> GetById(int id)
    {
        var item = await _svc.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Crear un nuevo registro diario.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SeguimientoLoteLevanteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeguimientoLoteLevanteDto>> Create([FromBody] CreateSeguimientoLoteLevanteRequest request)
    {
        if (request is null) return BadRequest(new { message = "Body requerido." });
        try
        {
            var dto = request.ToDto();
            var result = await _svc.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByLote), new { loteId = result.LoteId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            var pgEx = (PostgresException)ex.InnerException;
            if (string.Equals(pgEx.ConstraintName, "uq_seg_diario_aves_engorde_lote_fecha", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Ya existe un registro de seguimiento diario para este lote en la fecha seleccionada. Solo puede haber un registro por lote por día." });
            return BadRequest(new { message = "Error de duplicado en la base de datos.", detail = pgEx.Message });
        }
    }

    /// <summary>Editar un registro diario.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SeguimientoLoteLevanteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeguimientoLoteLevanteDto>> Update(int id, [FromBody] CreateSeguimientoLoteLevanteRequest request)
    {
        if (request is null) return BadRequest("Body requerido.");
        try
        {
            var dto = request.ToDto(id);
            var updated = await _svc.UpdateAsync(dto);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            var pgEx = (PostgresException)ex.InnerException;
            if (string.Equals(pgEx.ConstraintName, "uq_seg_diario_aves_engorde_lote_fecha", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Ya existe un registro de seguimiento diario para este lote en la fecha seleccionada. Solo puede haber un registro por lote por día." });
            return BadRequest(new { message = "Error de duplicado en la base de datos.", detail = pgEx.Message });
        }
    }

    /// <summary>Eliminar un registro diario.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _svc.DeleteAsync(id);
            if (!deleted)
                return NotFound(new { message = "Registro no encontrado o no tienes permisos para eliminarlo." });
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error al eliminar el registro.", error = ex.Message });
        }
    }

    /// <summary>Filtrar por fechas opcionalmente con loteId.</summary>
    [HttpGet("filtro")]
    [ProducesResponseType(typeof(IEnumerable<SeguimientoLoteLevanteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeguimientoLoteLevanteDto>>> Filter(
        [FromQuery] int? loteId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var result = await _svc.FilterAsync(loteId, desde, hasta);
        return Ok(result);
    }

    /// <summary>Resultado calculado (para Aves de Engorde devuelve lista vacía; estructura compatible con Levante).</summary>
    [HttpGet("por-lote/{loteId}/resultado")]
    [ProducesResponseType(typeof(ResultadoLevanteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoLevanteResponse>> GetResultadoPorLote(
        int loteId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] bool recalcular = true)
    {
        try
        {
            var res = await _svc.GetResultadoAsync(loteId, desde, hasta, recalcular);
            return Ok(res);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    public sealed record BackfillSeguimientoEngordeRequest(
        int LoteId,
        DateTime? Desde,
        DateTime? Hasta,
        bool OnlyIfMissing = true
    );

    /// <summary>
    /// Backfill masivo de metadata (Ingreso/Traslado/Documento/Despacho) desde lote_registro_historico_unificado.
    /// No aplica consumos ni movimientos de inventario; solo actualiza el jsonb metadata del seguimiento.
    /// </summary>
    [HttpPost("backfill-metadata")]
    [ProducesResponseType(typeof(SeguimientoAvesEngordeBackfillResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeguimientoAvesEngordeBackfillResultDto>> BackfillMetadata([FromBody] BackfillSeguimientoEngordeRequest req)
    {
        if (req is null) return BadRequest(new { message = "Body requerido." });
        if (req.LoteId <= 0) return BadRequest(new { message = "LoteId inválido." });
        if (req.Desde.HasValue && req.Hasta.HasValue && req.Desde.Value.Date > req.Hasta.Value.Date)
            return BadRequest(new { message = "Rango de fechas inválido (Desde > Hasta)." });

        try
        {
            var result = await _svc.BackfillMetadataAsync(
                loteId: req.LoteId,
                desde: req.Desde,
                hasta: req.Hasta,
                onlyIfMissing: req.OnlyIfMissing);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Valida las filas del Excel contra el histórico unificado del lote.
    /// Devuelve inconsistencias y acciones sugeridas sin modificar datos.
    /// </summary>
    [HttpPost("por-lote/{loteId}/cuadrar-saldos/validar")]
    [ProducesResponseType(typeof(CuadrarSaldosValidarResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CuadrarSaldosValidarResponseDto>> CuadrarSaldosValidar(
        int loteId,
        [FromBody] CuadrarSaldosValidarRequestDto req)
    {
        if (req?.FilasExcel == null || req.FilasExcel.Count == 0)
            return BadRequest(new { message = "Se requiere al menos una fila del Excel." });
        try
        {
            var result = await _svc.ValidarCuadrarSaldosAsync(loteId, req.FilasExcel);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Aplica correcciones sobre lote_registro_historico_unificado:
    /// ajusta fechas, anula sobrantes e inserta faltantes.
    /// No modifica stocks reales de inventario.
    /// </summary>
    [HttpPost("por-lote/{loteId}/cuadrar-saldos/aplicar")]
    [ProducesResponseType(typeof(CuadrarSaldosAplicarResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CuadrarSaldosAplicarResponseDto>> CuadrarSaldosAplicar(
        int loteId,
        [FromBody] CuadrarSaldosAplicarRequestDto req)
    {
        if (req?.Acciones == null || req.Acciones.Count == 0)
            return BadRequest(new { message = "Se requiere al menos una acción de corrección." });
        try
        {
            var result = await _svc.AplicarCuadrarSaldosAsync(loteId, req.Acciones, req.FilasExcel);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
