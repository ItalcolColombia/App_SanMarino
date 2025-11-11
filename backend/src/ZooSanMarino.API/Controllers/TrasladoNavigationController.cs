// src/ZooSanMarino.API/Controllers/TrasladoNavigationController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Controlador para navegación completa de traslados de aves y huevos
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Tags("Navegación de Traslados")]
public class TrasladoNavigationController : ControllerBase
{
    private readonly IMovimientoAvesService _movimientoService;
    private readonly ITrasladoHuevosService _trasladoHuevosService;
    private readonly ICurrentUser _currentUser;
    private readonly ZooSanMarinoContext _context;
    private readonly ILogger<TrasladoNavigationController> _logger;

    public TrasladoNavigationController(
        IMovimientoAvesService movimientoService,
        ITrasladoHuevosService trasladoHuevosService,
        ICurrentUser currentUser,
        ZooSanMarinoContext context,
        ILogger<TrasladoNavigationController> logger)
    {
        _movimientoService = movimientoService;
        _trasladoHuevosService = trasladoHuevosService;
        _currentUser = currentUser;
        _context = context;
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
    /// Obtiene movimientos y traslados por lote (aves y huevos) con navegación completa
    /// </summary>
    [HttpGet("por-lote/{loteId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<TrasladoUnificadoDto>))]
    public async Task<IActionResult> GetByLote(int loteId, [FromQuery] int limite = 100)
    {
        try
        {
            var loteIdStr = loteId.ToString();
            var resultados = new List<TrasladoUnificadoDto>();

            // 1. Obtener movimientos de aves (tanto origen como destino)
            var requestAvesOrigen = new MovimientoAvesCompletoSearchRequest
            {
                LoteOrigenId = loteId,
                PageSize = limite
            };
            
            var requestAvesDestino = new MovimientoAvesCompletoSearchRequest
            {
                LoteDestinoId = loteId,
                PageSize = limite
            };
            
            var movimientosAvesOrigen = await _movimientoService.SearchCompletoAsync(requestAvesOrigen);
            var movimientosAvesDestino = await _movimientoService.SearchCompletoAsync(requestAvesDestino);
            
            // Combinar y eliminar duplicados
            var movimientosAves = movimientosAvesOrigen.Items
                .UnionBy(movimientosAvesDestino.Items, m => m.Id)
                .ToList();
            
            // Convertir movimientos de aves a DTO unificado
            foreach (var mov in movimientosAves)
            {
                // Verificar fase del lote
                var faseLote = await DeterminarFaseLoteAsync(loteId);
                
                resultados.Add(new TrasladoUnificadoDto(
                    Id: mov.Id,
                    NumeroTraslado: mov.NumeroMovimiento,
                    FechaTraslado: mov.FechaMovimiento,
                    TipoOperacion: mov.TipoMovimiento,
                    TipoTraslado: "Aves",
                    LoteIdOrigen: mov.Origen.LoteId?.ToString() ?? loteIdStr,
                    LoteIdOrigenInt: mov.Origen.LoteId,
                    GranjaOrigenId: mov.Origen.GranjaId ?? 0,
                    GranjaOrigenNombre: mov.Origen.GranjaNombre,
                    LoteIdDestino: mov.Destino.LoteId?.ToString(),
                    LoteIdDestinoInt: mov.Destino.LoteId,
                    GranjaDestinoId: mov.Destino.GranjaId,
                    GranjaDestinoNombre: mov.Destino.GranjaNombre,
                    TipoDestino: null,
                    CantidadHembras: mov.CantidadHembras,
                    CantidadMachos: mov.CantidadMachos,
                    TotalAves: mov.TotalAves,
                    TotalHuevos: null,
                    CantidadLimpio: null,
                    CantidadTratado: null,
                    CantidadSucio: null,
                    CantidadDeforme: null,
                    CantidadBlanco: null,
                    CantidadDobleYema: null,
                    CantidadPiso: null,
                    CantidadPequeno: null,
                    CantidadRoto: null,
                    CantidadDesecho: null,
                    CantidadOtro: null,
                    Estado: mov.Estado,
                    Motivo: mov.MotivoMovimiento,
                    Descripcion: null,
                    Observaciones: mov.Observaciones,
                    UsuarioTrasladoId: mov.UsuarioMovimientoId,
                    UsuarioNombre: mov.UsuarioNombre,
                    FechaProcesamiento: mov.FechaProcesamiento,
                    FechaCancelacion: mov.FechaCancelacion,
                    CreatedAt: mov.CreatedAt,
                    UpdatedAt: mov.UpdatedAt,
                    FaseLote: faseLote,
                    TieneSeguimientoProduccion: await TieneSeguimientoProduccionAsync(loteId)
                ));
            }

            // 2. Obtener traslados de huevos
            var trasladosHuevos = await _trasladoHuevosService.ObtenerTrasladosPorLoteAsync(loteIdStr);
            
            // Obtener información de granjas para los traslados de huevos
            var granjaIds = trasladosHuevos
                .SelectMany(t => new[] { t.GranjaOrigenId, t.GranjaDestinoId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            
            var granjas = await _context.Farms
                .AsNoTracking()
                .Where(f => granjaIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.Name);

            // Convertir traslados de huevos a DTO unificado
            foreach (var traslado in trasladosHuevos.Take(limite))
            {
                // Verificar fase del lote
                var faseLote = await DeterminarFaseLoteAsync(loteId);
                
                resultados.Add(new TrasladoUnificadoDto(
                    Id: traslado.Id,
                    NumeroTraslado: traslado.NumeroTraslado,
                    FechaTraslado: traslado.FechaTraslado,
                    TipoOperacion: traslado.TipoOperacion,
                    TipoTraslado: "Huevos",
                    LoteIdOrigen: traslado.LoteId,
                    LoteIdOrigenInt: int.TryParse(traslado.LoteId, out var loteIdInt) ? loteIdInt : null,
                    GranjaOrigenId: traslado.GranjaOrigenId,
                    GranjaOrigenNombre: granjas.GetValueOrDefault(traslado.GranjaOrigenId),
                    LoteIdDestino: traslado.LoteDestinoId,
                    LoteIdDestinoInt: traslado.LoteDestinoId != null && int.TryParse(traslado.LoteDestinoId, out var loteDestInt) ? loteDestInt : null,
                    GranjaDestinoId: traslado.GranjaDestinoId,
                    GranjaDestinoNombre: traslado.GranjaDestinoId.HasValue ? granjas.GetValueOrDefault(traslado.GranjaDestinoId.Value) : null,
                    TipoDestino: traslado.TipoDestino,
                    CantidadHembras: null,
                    CantidadMachos: null,
                    TotalAves: null,
                    TotalHuevos: traslado.TotalHuevos,
                    CantidadLimpio: traslado.CantidadLimpio,
                    CantidadTratado: traslado.CantidadTratado,
                    CantidadSucio: traslado.CantidadSucio,
                    CantidadDeforme: traslado.CantidadDeforme,
                    CantidadBlanco: traslado.CantidadBlanco,
                    CantidadDobleYema: traslado.CantidadDobleYema,
                    CantidadPiso: traslado.CantidadPiso,
                    CantidadPequeno: traslado.CantidadPequeno,
                    CantidadRoto: traslado.CantidadRoto,
                    CantidadDesecho: traslado.CantidadDesecho,
                    CantidadOtro: traslado.CantidadOtro,
                    Estado: traslado.Estado,
                    Motivo: traslado.Motivo,
                    Descripcion: traslado.Descripcion,
                    Observaciones: traslado.Observaciones,
                    UsuarioTrasladoId: traslado.UsuarioTrasladoId,
                    UsuarioNombre: traslado.UsuarioNombre,
                    FechaProcesamiento: traslado.FechaProcesamiento,
                    FechaCancelacion: traslado.FechaCancelacion,
                    CreatedAt: traslado.CreatedAt,
                    UpdatedAt: traslado.UpdatedAt,
                    FaseLote: faseLote,
                    TieneSeguimientoProduccion: await TieneSeguimientoProduccionAsync(loteId)
                ));
            }

            // Ordenar por fecha descendente y limitar
            var resultadosOrdenados = resultados
                .OrderByDescending(r => r.FechaTraslado)
                .Take(limite)
                .ToList();

            _logger.LogInformation($"Retornando {resultadosOrdenados.Count} traslados para lote {loteId} ({resultadosOrdenados.Count(r => r.TipoTraslado == "Aves")} aves, {resultadosOrdenados.Count(r => r.TipoTraslado == "Huevos")} huevos)");

            return Ok(resultadosOrdenados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimientos por lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
        }
    }

    /// <summary>
    /// Determina la fase del lote (Levante o Producción) basándose en seguimiento de producción
    /// </summary>
    private async Task<string> DeterminarFaseLoteAsync(int loteId)
    {
        try
        {
            var loteIdStr = loteId.ToString();
            
            // Verificar si tiene registros en seguimiento de producción
            var tieneSeguimiento = await _context.SeguimientoProduccion
                .AsNoTracking()
                .AnyAsync(s => s.LoteId == loteIdStr);
            
            if (tieneSeguimiento)
            {
                return "Produccion";
            }
            
            // Verificar si tiene registro en ProduccionLote
            var tieneProduccionLote = await _context.ProduccionLotes
                .AsNoTracking()
                .AnyAsync(p => p.LoteId == loteIdStr && p.DeletedAt == null);
            
            if (tieneProduccionLote)
            {
                return "Produccion";
            }
            
            return "Levante";
        }
        catch
        {
            return "Levante"; // Por defecto
        }
    }

    /// <summary>
    /// Verifica si el lote tiene seguimiento de producción
    /// </summary>
    private async Task<bool> TieneSeguimientoProduccionAsync(int loteId)
    {
        try
        {
            var loteIdStr = loteId.ToString();
            return await _context.SeguimientoProduccion
                .AsNoTracking()
                .AnyAsync(s => s.LoteId == loteIdStr);
        }
        catch
        {
            return false;
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





