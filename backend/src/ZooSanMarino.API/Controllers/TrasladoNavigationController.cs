// src/ZooSanMarino.API/Controllers/TrasladoNavigationController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Controlador para navegación completa de traslados de aves
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Tags("Navegación de Traslados")]
public class TrasladoNavigationController : ControllerBase
{
    private readonly IMovimientoAvesService _movimientoService;
    private readonly ILogger<TrasladoNavigationController> _logger;

    public TrasladoNavigationController(
        IMovimientoAvesService movimientoService,
        ILogger<TrasladoNavigationController> logger)
    {
        _movimientoService = movimientoService;
        _logger = logger;
    }

    /// <summary>
    /// Busca movimientos con navegación completa
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ZooSanMarino.Application.DTOs.Common.PagedResult<MovimientoAvesCompletoDto>))]
    public async Task<IActionResult> SearchCompleto([FromBody] MovimientoAvesCompletoSearchRequest request)
    {
        try
        {
            var result = await _movimientoService.SearchCompletoAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar movimientos con navegación completa");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene un movimiento específico con navegación completa
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MovimientoAvesCompletoDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCompletoById(int id)
    {
        try
        {
            var movimiento = await _movimientoService.GetCompletoByIdAsync(id);
            if (movimiento == null)
                return NotFound(new { error = $"Movimiento con ID {id} no encontrado" });

            return Ok(movimiento);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimiento completo {Id}", id);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene resúmenes de traslados recientes para dashboard
    /// </summary>
    [HttpGet("resumenes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<ResumenTrasladoDto>))]
    public async Task<IActionResult> GetResumenesRecientes([FromQuery] int dias = 7, [FromQuery] int limite = 10)
    {
        try
        {
            var resumenes = await _movimientoService.GetResumenesRecientesAsync(dias, limite);
            return Ok(resumenes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener resúmenes de traslados");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene estadísticas completas de traslados
    /// </summary>
    [HttpGet("estadisticas")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(EstadisticasTrasladoDto))]
    public async Task<IActionResult> GetEstadisticasCompletas([FromQuery] DateTime? fechaDesde = null, [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            var estadisticas = await _movimientoService.GetEstadisticasCompletasAsync(fechaDesde, fechaHasta);
            return Ok(estadisticas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener estadísticas de traslados");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene movimientos por granja con navegación completa
    /// </summary>
    [HttpGet("por-granja/{granjaId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<MovimientoAvesCompletoDto>))]
    public async Task<IActionResult> GetByGranja(int granjaId, [FromQuery] int limite = 50)
    {
        try
        {
            var request = new MovimientoAvesCompletoSearchRequest
            {
                GranjaOrigenId = granjaId,
                PageSize = limite
            };
            
            var result = await _movimientoService.SearchCompletoAsync(request);
            return Ok(result.Items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimientos por granja {GranjaId}", granjaId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene movimientos por lote con navegación completa
    /// </summary>
    [HttpGet("por-lote/{loteId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<MovimientoAvesCompletoDto>))]
    public async Task<IActionResult> GetByLote(int loteId, [FromQuery] int limite = 50)
    {
        try
        {
            var request = new MovimientoAvesCompletoSearchRequest
            {
                LoteOrigenId = loteId,
                PageSize = limite
            };
            
            var result = await _movimientoService.SearchCompletoAsync(request);
            return Ok(result.Items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimientos por lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene movimientos pendientes con navegación completa
    /// </summary>
    [HttpGet("pendientes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<MovimientoAvesCompletoDto>))]
    public async Task<IActionResult> GetPendientes([FromQuery] int limite = 50)
    {
        try
        {
            var request = new MovimientoAvesCompletoSearchRequest
            {
                Estado = "Pendiente",
                PageSize = limite
            };
            
            var result = await _movimientoService.SearchCompletoAsync(request);
            return Ok(result.Items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimientos pendientes");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene movimientos por tipo con navegación completa
    /// </summary>
    [HttpGet("por-tipo/{tipoMovimiento}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<MovimientoAvesCompletoDto>))]
    public async Task<IActionResult> GetByTipo(string tipoMovimiento, [FromQuery] int limite = 50)
    {
        try
        {
            var request = new MovimientoAvesCompletoSearchRequest
            {
                TipoMovimiento = tipoMovimiento,
                PageSize = limite
            };
            
            var result = await _movimientoService.SearchCompletoAsync(request);
            return Ok(result.Items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimientos por tipo {TipoMovimiento}", tipoMovimiento);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }
}





