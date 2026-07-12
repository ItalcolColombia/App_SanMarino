// src/ZooSanMarino.API/Controllers/MovimientoAvesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Controlador para gestión de movimientos y traslados de aves.
/// Orquestador delgado: valida entrada, delega en <see cref="IMovimientoAvesService"/> y mapea
/// resultado → status code. La lógica de datos/aritmética vive en Application/Infrastructure.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Tags("Movimiento de Aves")]
public class MovimientoAvesController : ControllerBase
{
    private readonly IMovimientoAvesService _movimientoService;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<MovimientoAvesController> _logger;

    public MovimientoAvesController(
        IMovimientoAvesService movimientoService,
        ICurrentUser currentUser,
        ILogger<MovimientoAvesController> logger)
    {
        _movimientoService = movimientoService;
        _currentUser = currentUser;
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
    /// Obtiene el último número de despacho generado (para Ecuador)
    /// </summary>
    [HttpGet("ultimo-numero-despacho")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUltimoNumeroDespacho()
    {
        try
        {
            var resultado = await _movimientoService.ObtenerUltimoNumeroDespachoAsync();
            return Ok(new { ultimoId = resultado.UltimoId, siguienteNumero = resultado.SiguienteNumero });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener último número de despacho");
            return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene información del lote para movimientos (etapa, aves disponibles, etc.)
    /// Calcula las aves actuales desde los registros diarios de seguimiento (Producción o Levante)
    /// </summary>
    [HttpGet("lote/{loteId}/informacion")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InformacionLoteMovimientoDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInformacionLote(int loteId)
    {
        try
        {
            var info = await _movimientoService.ObtenerInformacionLoteAsync(loteId);
            if (info == null)
                return NotFound(new { error = $"Lote {loteId} no encontrado" });

            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener información del lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
        }
    }

    /// <summary>
    /// Valida que exista un registro de Seguimiento Diario para el lote en la fecha indicada.
    /// Requisito previo obligatorio para registrar cualquier movimiento de aves.
    /// </summary>
    [HttpGet("lote/{loteId}/validar-fecha-seguimiento")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ValidarFechaSeguimiento(int loteId, [FromQuery] DateTime fecha)
    {
        try
        {
            var resultado = await _movimientoService.ValidarFechaSeguimientoAsync(loteId, fecha);
            if (resultado is null)
                return NotFound(new { error = $"Lote {loteId} no encontrado" });

            if (!resultado.Existe)
            {
                var fechaDisplay = resultado.FechaNormalizada.ToString("dd/MM/yyyy");
                return UnprocessableEntity(new
                {
                    existe = false,
                    error = $"No existe registro de Seguimiento Diario para el lote {loteId} en la fecha {fechaDisplay}. " +
                            "Cree el seguimiento del día antes de registrar el movimiento.",
                    loteId,
                    fecha = resultado.FechaNormalizada,
                    tipoLote = resultado.TipoLote
                });
            }

            return Ok(new
            {
                existe = true,
                seguimientoId = resultado.SeguimientoId,
                tipoSeguimiento = resultado.TipoSeguimiento,
                lotePosturaLevanteId = resultado.LotePosturaLevanteId,
                lotePosturaProduccionId = resultado.LotePosturaProduccionId,
                fecha = resultado.Fecha,
                trasladoAvesEntrante = resultado.TrasladoAvesEntrante,
                trasladoAvesSalida = resultado.TrasladoAvesSalida,
                ventaAvesCantidad = resultado.VentaAvesCantidad,
                ventaAvesMotivo = resultado.VentaAvesMotivo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar fecha de seguimiento para lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
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
    /// Elimina (lógicamente) un movimiento y revierte su efecto sobre el inventario de aves
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResultadoMovimientoDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id)
    {
        try
        {
            var resultado = await _movimientoService.EliminarMovimientoAsync(id);
            if (!resultado.Success)
                return BadRequest(resultado);
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar movimiento {Id}", id);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Actualiza un movimiento pendiente
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MovimientoAvesDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarMovimientoAvesDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var movimiento = await _movimientoService.ActualizarMovimientoAvesAsync(id, dto, _currentUser.UserId);
            return Ok(movimiento);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar movimiento {Id}", id);
            return StatusCode(500, new { message = "Error interno del servidor", details = ex.Message });
        }
    }

    /// <summary>
    /// Cancela un movimiento pendiente o completado
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
                MotivoCancelacion = request.MotivoEfectivo
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
    [HttpPost("ejecutar-venta")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResultadoMovimientoDto))]
    public async Task<IActionResult> EjecutarVenta([FromBody] EjecutarVentaAvesRequest request)
    {
        try
        {
            var resultado = await _movimientoService.EjecutarVentaAsync(request);
            return resultado.Success ? Ok(resultado) : BadRequest(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar venta");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    [HttpPost("ejecutar-traslado")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResultadoMovimientoDto))]
    public async Task<IActionResult> EjecutarTraslado([FromBody] EjecutarTrasladoAvesRequest request)
    {
        try
        {
            var resultado = await _movimientoService.EjecutarTrasladoAsync(request);
            return resultado.Success ? Ok(resultado) : BadRequest(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar traslado");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    [HttpPost("ejecutar-traslado-cierre-levante")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResultadoMovimientoDto))]
    public async Task<IActionResult> EjecutarTrasladoCierreLevante([FromBody] TrasladoCierreLevanteRequest request)
    {
        try
        {
            var resultado = await _movimientoService.EjecutarTrasladoCierreLevanteAsync(request);
            return resultado.Success ? Ok(resultado) : BadRequest(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar traslado de cierre levante");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

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
    /// <summary>Compatibilidad con clientes que envían <c>motivoCancelacion</c>.</summary>
    public string? MotivoCancelacion { get; set; }

    public string? Motivo { get; set; }

    public string MotivoEfectivo => MotivoCancelacion?.Trim() ?? Motivo?.Trim() ?? "Cancelado por usuario";
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
