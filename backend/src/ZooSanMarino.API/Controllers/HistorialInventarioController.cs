// src/ZooSanMarino.API/Controllers/HistorialInventarioController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Controlador para consulta del historial de inventarios
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Tags("Historial de Inventario")]
public class HistorialInventarioController : ControllerBase
{
    private readonly IHistorialInventarioService _historialService;
    private readonly ILogger<HistorialInventarioController> _logger;

    public HistorialInventarioController(
        IHistorialInventarioService historialService,
        ILogger<HistorialInventarioController> logger)
    {
        _historialService = historialService;
        _logger = logger;
    }

    /// <summary>
    /// Busca historial con filtros y paginación
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ZooSanMarino.Application.DTOs.Common.PagedResult<HistorialInventarioDto>))]
    public async Task<IActionResult> Search([FromBody] HistorialInventarioSearchRequest request)
    {
        try
        {
            var result = await _historialService.SearchAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar historial de inventario");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene historial por inventario
    /// </summary>
    [HttpGet("inventario/{inventarioId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<HistorialInventarioDto>))]
    public async Task<IActionResult> GetByInventario(int inventarioId)
    {
        try
        {
            var historial = await _historialService.GetByInventarioIdAsync(inventarioId);
            return Ok(historial);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener historial del inventario {InventarioId}", inventarioId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene historial por lote
    /// </summary>
    [HttpGet("lote/{loteId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<HistorialInventarioDto>))]
    public async Task<IActionResult> GetByLote(int loteId)  // Changed from string to int
    {
        try
        {
            var historial = await _historialService.GetByLoteIdAsync(loteId);  // Changed from loteId
            return Ok(historial);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener historial del lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene historial por movimiento
    /// </summary>
    [HttpGet("movimiento/{movimientoId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<HistorialInventarioDto>))]
    public async Task<IActionResult> GetByMovimiento(int movimientoId)
    {
        try
        {
            var historial = await _historialService.GetByMovimientoIdAsync(movimientoId);
            return Ok(historial);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener historial del movimiento {MovimientoId}", movimientoId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene trazabilidad completa de un lote
    /// </summary>
    [HttpGet("trazabilidad/{loteId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TrazabilidadLoteDto))]
    public async Task<IActionResult> GetTrazabilidad(int loteId)  // Changed from string to int
    {
        try
        {
            var trazabilidad = await _historialService.GetTrazabilidadLoteAsync(loteId);  // Changed from loteId
            return Ok(trazabilidad);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener trazabilidad del lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene eventos de trazabilidad por lote
    /// </summary>
    [HttpGet("eventos/{loteId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<EventoTrazabilidadDto>))]
    public async Task<IActionResult> GetEventos(int loteId, [FromQuery] DateTime? fechaDesde = null, [FromQuery] DateTime? fechaHasta = null)  // Changed from string to int
    {
        try
        {
            var eventos = await _historialService.GetEventosLoteAsync(loteId, fechaDesde, fechaHasta);  // Changed from loteId
            return Ok(eventos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener eventos del lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene resumen de cambios por período
    /// </summary>
    [HttpGet("resumen-cambios")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResumenCambiosDto))]
    public async Task<IActionResult> GetResumenCambios(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta,
        [FromQuery] int? granjaId = null)
    {
        try
        {
            var resumen = await _historialService.GetResumenCambiosAsync(fechaDesde, fechaHasta, granjaId);
            return Ok(resumen);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener resumen de cambios");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene cambios recientes
    /// </summary>
    [HttpGet("recientes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<HistorialInventarioDto>))]
    public async Task<IActionResult> GetCambiosRecientes([FromQuery] int dias = 7)
    {
        try
        {
            var cambios = await _historialService.GetCambiosRecientesAsync(dias);
            return Ok(cambios);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener cambios recientes");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene cambios por usuario
    /// </summary>
    [HttpGet("usuario/{usuarioId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<HistorialInventarioDto>))]
    public async Task<IActionResult> GetCambiosPorUsuario(
        int usuarioId,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            var cambios = await _historialService.GetCambiosPorUsuarioAsync(usuarioId, fechaDesde, fechaHasta);
            return Ok(cambios);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener cambios del usuario {UsuarioId}", usuarioId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene ajustes de inventario
    /// </summary>
    [HttpGet("ajustes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<HistorialInventarioDto>))]
    public async Task<IActionResult> GetAjustes(
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            var ajustes = await _historialService.GetAjustesInventarioAsync(fechaDesde, fechaHasta);
            return Ok(ajustes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener ajustes de inventario");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene movimientos grandes (por encima de un mínimo)
    /// </summary>
    [HttpGet("movimientos-grandes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<HistorialInventarioDto>))]
    public async Task<IActionResult> GetMovimientosGrandes([FromQuery] int minimoAves = 1000)
    {
        try
        {
            var movimientos = await _historialService.GetMovimientosGrandesAsync(minimoAves);
            return Ok(movimientos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimientos grandes");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }
}
