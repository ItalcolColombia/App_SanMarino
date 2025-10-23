// src/ZooSanMarino.API/Controllers/MovimientoAvesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Controlador para gestión de movimientos y traslados de aves
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Tags("Movimiento de Aves")]
public class MovimientoAvesController : ControllerBase
{
    private readonly IMovimientoAvesService _movimientoService;
    private readonly ILogger<MovimientoAvesController> _logger;

    public MovimientoAvesController(
        IMovimientoAvesService movimientoService,
        ILogger<MovimientoAvesController> logger)
    {
        _movimientoService = movimientoService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los movimientos
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<MovimientoAvesDto>))]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var movimientos = await _movimientoService.GetAllAsync();
            return Ok(movimientos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimientos");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Busca movimientos con filtros y paginación
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ZooSanMarino.Application.DTOs.Common.PagedResult<MovimientoAvesDto>))]
    public async Task<IActionResult> Search([FromBody] MovimientoAvesSearchRequest request)
    {
        try
        {
            var result = await _movimientoService.SearchAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar movimientos");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene un movimiento por ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MovimientoAvesDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var movimiento = await _movimientoService.GetByIdAsync(id);
            if (movimiento == null)
                return NotFound(new { error = $"Movimiento con ID {id} no encontrado" });

            return Ok(movimiento);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimiento {Id}", id);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene un movimiento por número de movimiento
    /// </summary>
    [HttpGet("numero/{numeroMovimiento}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MovimientoAvesDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByNumero(string numeroMovimiento)
    {
        try
        {
            var movimiento = await _movimientoService.GetByNumeroMovimientoAsync(numeroMovimiento);
            if (movimiento == null)
                return NotFound(new { error = $"Movimiento {numeroMovimiento} no encontrado" });

            return Ok(movimiento);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimiento {NumeroMovimiento}", numeroMovimiento);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene movimientos pendientes
    /// </summary>
    [HttpGet("pendientes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<MovimientoAvesDto>))]
    public async Task<IActionResult> GetPendientes()
    {
        try
        {
            var movimientos = await _movimientoService.GetMovimientosPendientesAsync();
            return Ok(movimientos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimientos pendientes");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene movimientos por lote
    /// </summary>
    [HttpGet("lote/{loteId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<MovimientoAvesDto>))]
    public async Task<IActionResult> GetByLote(int loteId)  // Changed from string to int
    {
        try
        {
            var movimientos = await _movimientoService.GetMovimientosByLoteAsync(loteId);  // Changed from loteId
            return Ok(movimientos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimientos del lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene movimientos por usuario
    /// </summary>
    [HttpGet("usuario/{usuarioId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<MovimientoAvesDto>))]
    public async Task<IActionResult> GetByUsuario(int usuarioId)
    {
        try
        {
            var movimientos = await _movimientoService.GetMovimientosByUsuarioAsync(usuarioId);
            return Ok(movimientos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimientos del usuario {UsuarioId}", usuarioId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene movimientos recientes
    /// </summary>
    [HttpGet("recientes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<MovimientoAvesDto>))]
    public async Task<IActionResult> GetRecientes([FromQuery] int dias = 7)
    {
        try
        {
            var movimientos = await _movimientoService.GetMovimientosRecientesAsync(dias);
            return Ok(movimientos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener movimientos recientes");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Crea un nuevo movimiento
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(MovimientoAvesDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateMovimientoAvesDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var movimiento = await _movimientoService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = movimiento.Id }, movimiento);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear movimiento");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Procesa un movimiento pendiente
    /// </summary>
    [HttpPost("{id}/procesar")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResultadoMovimientoDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Procesar(int id, [FromBody] ProcesarMovimientoRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var procesarDto = new ProcesarMovimientoDto
            {
                MovimientoId = id,
                ObservacionesProcesamiento = request.Observaciones,
                AutoCrearInventarioDestino = request.AutoCrearInventarioDestino
            };

            var resultado = await _movimientoService.ProcesarMovimientoAsync(procesarDto);
            
            if (!resultado.Success)
                return BadRequest(resultado);

            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar movimiento {Id}", id);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Cancela un movimiento pendiente
    /// </summary>
    [HttpPost("{id}/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResultadoMovimientoDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancelar(int id, [FromBody] CancelarMovimientoRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var cancelarDto = new CancelarMovimientoDto
            {
                MovimientoId = id,
                MotivoCancelacion = request.Motivo
            };

            var resultado = await _movimientoService.CancelarMovimientoAsync(cancelarDto);
            
            if (!resultado.Success)
                return BadRequest(resultado);

            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cancelar movimiento {Id}", id);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Realiza un traslado rápido entre ubicaciones
    /// </summary>
    [HttpPost("traslado-rapido")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ResultadoMovimientoDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TrasladoRapido([FromBody] TrasladoRapidoRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var trasladoDto = new TrasladoRapidoDto
            {
                LoteId = int.Parse(request.LoteId),  // Convert string to int
                GranjaOrigenId = request.GranjaOrigenId,
                NucleoOrigenId = request.NucleoOrigenId,
                GalponOrigenId = request.GalponOrigenId,
                GranjaDestinoId = request.GranjaDestinoId,
                NucleoDestinoId = request.NucleoDestinoId,
                GalponDestinoId = request.GalponDestinoId,
                CantidadHembras = request.CantidadHembras,
                CantidadMachos = request.CantidadMachos,
                CantidadMixtas = request.CantidadMixtas,
                MotivoTraslado = request.Motivo,
                Observaciones = request.Observaciones,
                ProcesarInmediatamente = request.ProcesarInmediatamente
            };

            var resultado = await _movimientoService.TrasladoRapidoAsync(trasladoDto);
            
            if (!resultado.Success)
                return BadRequest(resultado);

            return CreatedAtAction(nameof(GetById), new { id = resultado.MovimientoId }, resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en traslado rápido");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Valida si un movimiento es posible
    /// </summary>
    [HttpPost("validar")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ValidacionMovimientoDto))]
    public async Task<IActionResult> ValidarMovimiento([FromBody] CreateMovimientoAvesDto dto)
    {
        try
        {
            var esValido = await _movimientoService.ValidarMovimientoAsync(dto);
            var errores = new List<string>();

            if (dto.InventarioOrigenId.HasValue)
            {
                var erroresDisponibilidad = await _movimientoService.ValidarDisponibilidadAvesAsync(
                    dto.InventarioOrigenId.Value, 
                    dto.CantidadHembras, 
                    dto.CantidadMachos, 
                    dto.CantidadMixtas);
                errores.AddRange(erroresDisponibilidad);
            }

            if (dto.GranjaDestinoId.HasValue)
            {
                var ubicacionValida = await _movimientoService.ValidarUbicacionDestinoAsync(
                    dto.GranjaDestinoId.Value, 
                    dto.NucleoDestinoId, 
                    dto.GalponDestinoId);
                
                if (!ubicacionValida)
                    errores.Add("La ubicación de destino no es válida");
            }

            var resultado = new ValidacionMovimientoDto
            {
                EsValido = esValido && !errores.Any(),
                Errores = errores,
                Advertencias = new List<string>()
            };

            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar movimiento");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene estadísticas de movimientos
    /// </summary>
    [HttpGet("estadisticas")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(EstadisticasMovimientoDto))]
    public async Task<IActionResult> GetEstadisticas([FromQuery] DateTime? fechaDesde = null, [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            var totalPendientes = await _movimientoService.GetTotalMovimientosPendientesAsync();
            var totalCompletados = await _movimientoService.GetTotalMovimientosCompletadosAsync(fechaDesde, fechaHasta);

            var estadisticas = new EstadisticasMovimientoDto
            {
                TotalPendientes = totalPendientes,
                TotalCompletados = totalCompletados,
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta
            };

            return Ok(estadisticas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener estadísticas de movimientos");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }
}

// =====================================================
// REQUEST MODELS PARA EL CONTROLADOR
// =====================================================

/// <summary>
/// Request para procesar un movimiento
/// </summary>
public sealed class ProcesarMovimientoRequest
{
    public string? Observaciones { get; set; }
    public bool AutoCrearInventarioDestino { get; set; } = true;
}

/// <summary>
/// Request para cancelar un movimiento
/// </summary>
public sealed class CancelarMovimientoRequest
{
    public string Motivo { get; set; } = null!;
}

/// <summary>
/// Request para traslado rápido
/// </summary>
public sealed class TrasladoRapidoRequest
{
    public string LoteId { get; set; } = null!;
    public int? GranjaOrigenId { get; set; }
    public string? NucleoOrigenId { get; set; }
    public string? GalponOrigenId { get; set; }
    public int GranjaDestinoId { get; set; }
    public string? NucleoDestinoId { get; set; }
    public string? GalponDestinoId { get; set; }
    public int? CantidadHembras { get; set; }
    public int? CantidadMachos { get; set; }
    public int? CantidadMixtas { get; set; }
    public string? Motivo { get; set; }
    public string? Observaciones { get; set; }
    public bool ProcesarInmediatamente { get; set; } = true;
}

/// <summary>
/// DTO para validación de movimientos
/// </summary>
public sealed class ValidacionMovimientoDto
{
    public bool EsValido { get; set; }
    public List<string> Errores { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
}

/// <summary>
/// DTO para estadísticas de movimientos
/// </summary>
public sealed class EstadisticasMovimientoDto
{
    public int TotalPendientes { get; set; }
    public int TotalCompletados { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
}
