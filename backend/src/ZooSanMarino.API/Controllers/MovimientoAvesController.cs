// src/ZooSanMarino.API/Controllers/MovimientoAvesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

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
    private readonly IInventarioAvesService _inventarioService;
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<MovimientoAvesController> _logger;

    public MovimientoAvesController(
        IMovimientoAvesService movimientoService,
        IInventarioAvesService inventarioService,
        ZooSanMarinoContext context,
        ICurrentUser currentUser,
        ILogger<MovimientoAvesController> logger)
    {
        _movimientoService = movimientoService;
        _inventarioService = inventarioService;
        _context = context;
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
            // Obtener el último movimiento de tipo despacho (o el último movimiento en general)
            var ultimoMovimiento = await _context.MovimientoAves
                .AsNoTracking()
                .Where(m => m.CompanyId == _currentUser.CompanyId && 
                           m.DeletedAt == null)
                .OrderByDescending(m => m.Id)
                .FirstOrDefaultAsync();

            var ultimoId = ultimoMovimiento?.Id ?? 0;
            var siguienteNumero = ultimoId + 1;

            return Ok(new { ultimoId = ultimoId, siguienteNumero = siguienteNumero });
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInformacionLote(int loteId)
    {
        try
        {
            var ctx = await ObtenerContextoLoteAsync(loteId);
            if (ctx == null)
                return NotFound(new { error = $"Lote {loteId} no encontrado" });

            var lote = ctx.Lote;
            var etapa = ctx.Etapa;
            var tipoLote = ctx.TipoLote;
            var lotePosturaLev = ctx.LotePosturaLevante;

            int hembrasIniciales = 0;
            int machosIniciales = 0;
            int hembrasActuales = 0;
            int machosActuales = 0;
            int mixtasActuales = 0;

            if (tipoLote == "Produccion")
            {
                var lotePosturaProd = ctx.LotePosturaProduccion;

                if (lotePosturaProd != null)
                {
                    hembrasIniciales = lotePosturaProd.AvesHInicial ?? lotePosturaProd.HembrasInicialesProd ?? 0;
                    machosIniciales = lotePosturaProd.AvesMInicial ?? lotePosturaProd.MachosInicialesProd ?? 0;

                    var mortalidadProd = await _context.SeguimientoProduccion
                        .AsNoTracking()
                        .Where(s => s.LoteId == loteId)
                        .GroupBy(_ => 1)
                        .Select(g => new {
                            MortH = g.Sum(x => (int?)x.MortalidadH) ?? 0,
                            MortM = g.Sum(x => (int?)x.MortalidadM) ?? 0,
                            SelH  = g.Sum(x => (int?)x.SelH) ?? 0,
                            SelM  = g.Sum(x => (int?)x.SelM) ?? 0
                        })
                        .FirstOrDefaultAsync();

                    var movSalidaProd = await _context.MovimientoAves
                        .AsNoTracking()
                        .Where(m => m.LoteOrigenId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                        .ToListAsync();

                    var movEntradaProd = await _context.MovimientoAves
                        .AsNoTracking()
                        .Where(m => m.LoteDestinoId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                        .ToListAsync();

                    hembrasActuales = Math.Max(0, hembrasIniciales
                        - (mortalidadProd?.MortH ?? 0) - (mortalidadProd?.SelH ?? 0)
                        - movSalidaProd.Sum(m => m.CantidadHembras)
                        + movEntradaProd.Sum(m => m.CantidadHembras));
                    machosActuales = Math.Max(0, machosIniciales
                        - (mortalidadProd?.MortM ?? 0) - (mortalidadProd?.SelM ?? 0)
                        - movSalidaProd.Sum(m => m.CantidadMachos)
                        + movEntradaProd.Sum(m => m.CantidadMachos));
                    mixtasActuales = movEntradaProd.Sum(m => m.CantidadMixtas);
                }
                else
                {
                    var loteProd = lote.Fase == "Produccion" ? lote : await _context.Lotes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(l => l.LotePadreId == loteId && l.Fase == "Produccion" && l.DeletedAt == null);

                    if (loteProd != null)
                    {
                        hembrasIniciales = loteProd.HembrasInicialesProd ?? 0;
                        machosIniciales = loteProd.MachosInicialesProd ?? 0;
                        var loteIdSeguimiento = loteProd.LoteId ?? loteId;

                        var seguimientos = await _context.SeguimientoProduccion
                            .AsNoTracking()
                            .Where(s => s.LoteId == loteIdSeguimiento)
                            .ToListAsync();

                        var totalMortalidadH = seguimientos.Sum(s => s.MortalidadH);
                        var totalMortalidadM = seguimientos.Sum(s => s.MortalidadM);
                        var totalSeleccionH = seguimientos.Sum(s => s.SelH);
                        var totalSeleccionM = seguimientos.Sum(s => s.SelM);

                        var movimientosSalida = await _context.MovimientoAves
                            .AsNoTracking()
                            .Where(m => m.LoteOrigenId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                            .ToListAsync();

                        var movimientosEntrada = await _context.MovimientoAves
                            .AsNoTracking()
                            .Where(m => m.LoteDestinoId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                            .ToListAsync();

                        hembrasActuales = Math.Max(0, hembrasIniciales - totalMortalidadH - totalSeleccionH
                            - movimientosSalida.Sum(m => m.CantidadHembras)
                            + movimientosEntrada.Sum(m => m.CantidadHembras));
                        machosActuales = Math.Max(0, machosIniciales - totalMortalidadM - totalSeleccionM
                            - movimientosSalida.Sum(m => m.CantidadMachos)
                            + movimientosEntrada.Sum(m => m.CantidadMachos));
                        mixtasActuales = movimientosEntrada.Sum(m => m.CantidadMixtas);
                    }
                }
            }
            else
            {
                if (lotePosturaLev != null)
                {
                    hembrasIniciales = lotePosturaLev.AvesHInicial ?? 0;
                    machosIniciales = lotePosturaLev.AvesMInicial ?? 0;
                    var mortCajaHLev = lote.MortCajaH ?? 0;
                    var mortCajaMlev = lote.MortCajaM ?? 0;

                    var mortalidadLev = await _context.SeguimientoLoteLevante
                        .AsNoTracking()
                        .Where(s => s.LoteId == loteId)
                        .GroupBy(_ => 1)
                        .Select(g => new {
                            MortH = g.Sum(x => (int?)x.MortalidadHembras) ?? 0,
                            MortM = g.Sum(x => (int?)x.MortalidadMachos) ?? 0,
                            SelH  = g.Sum(x => (int?)x.SelH) ?? 0,
                            SelM  = g.Sum(x => (int?)x.SelM) ?? 0,
                            ErrH  = g.Sum(x => (int?)x.ErrorSexajeHembras) ?? 0,
                            ErrM  = g.Sum(x => (int?)x.ErrorSexajeMachos) ?? 0
                        })
                        .FirstOrDefaultAsync();

                    var movSalidaLev = await _context.MovimientoAves
                        .AsNoTracking()
                        .Where(m => m.LoteOrigenId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                        .ToListAsync();

                    var movEntradaLev = await _context.MovimientoAves
                        .AsNoTracking()
                        .Where(m => m.LoteDestinoId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                        .ToListAsync();

                    hembrasActuales = Math.Max(0, hembrasIniciales - mortCajaHLev
                        - (mortalidadLev?.MortH ?? 0) - (mortalidadLev?.SelH ?? 0) - (mortalidadLev?.ErrH ?? 0)
                        - movSalidaLev.Sum(m => m.CantidadHembras)
                        + movEntradaLev.Sum(m => m.CantidadHembras));
                    machosActuales = Math.Max(0, machosIniciales - mortCajaMlev
                        - (mortalidadLev?.MortM ?? 0) - (mortalidadLev?.SelM ?? 0) - (mortalidadLev?.ErrM ?? 0)
                        - movSalidaLev.Sum(m => m.CantidadMachos)
                        + movEntradaLev.Sum(m => m.CantidadMachos));
                    mixtasActuales = movEntradaLev.Sum(m => m.CantidadMixtas);
                }
                else
                {
                    hembrasIniciales = lote.HembrasL ?? 0;
                    machosIniciales = lote.MachosL ?? 0;
                    var mortCajaH = lote.MortCajaH ?? 0;
                    var mortCajaM = lote.MortCajaM ?? 0;

                    var seguimientos = await _context.SeguimientoLoteLevante
                        .AsNoTracking()
                        .Where(s => s.LoteId == loteId)
                        .ToListAsync();

                    var totalMortalidadH = seguimientos.Sum(s => s.MortalidadHembras);
                    var totalMortalidadM = seguimientos.Sum(s => s.MortalidadMachos);
                    var totalSeleccionH = seguimientos.Sum(s => s.SelH);
                    var totalSeleccionM = seguimientos.Sum(s => s.SelM);
                    var totalErrorSexajeH = seguimientos.Sum(s => s.ErrorSexajeHembras);
                    var totalErrorSexajeM = seguimientos.Sum(s => s.ErrorSexajeMachos);

                    var movimientosSalida = await _context.MovimientoAves
                        .AsNoTracking()
                        .Where(m => m.LoteOrigenId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                        .ToListAsync();

                    var movimientosEntrada = await _context.MovimientoAves
                        .AsNoTracking()
                        .Where(m => m.LoteDestinoId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                        .ToListAsync();

                    hembrasActuales = Math.Max(0, hembrasIniciales - mortCajaH - totalMortalidadH - totalSeleccionH - totalErrorSexajeH
                        - movimientosSalida.Sum(m => m.CantidadHembras)
                        + movimientosEntrada.Sum(m => m.CantidadHembras));
                    machosActuales = Math.Max(0, machosIniciales - mortCajaM - totalMortalidadM - totalSeleccionM - totalErrorSexajeM
                        - movimientosSalida.Sum(m => m.CantidadMachos)
                        + movimientosEntrada.Sum(m => m.CantidadMachos));
                    mixtasActuales = movimientosEntrada.Sum(m => m.CantidadMixtas);
                }
            }

            var totalAvesActuales = hembrasActuales + machosActuales + mixtasActuales;

            DateTime? fechaInicioProduccion = null;
            if (tipoLote == "Produccion")
            {
                var loteProdFecha = lote.Fase == "Produccion" ? lote : await _context.Lotes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.LotePadreId == loteId && l.Fase == "Produccion" && l.DeletedAt == null);
                fechaInicioProduccion = loteProdFecha?.FechaInicioProduccion;
            }

            string? raza = lote!.Raza;
            int? anoTablaGenetica = lote.AnoTablaGenetica;
            if (lote.LotePadreId.HasValue && (string.IsNullOrEmpty(raza) || !anoTablaGenetica.HasValue))
            {
                var lotePadre = await _context.Lotes
                    .AsNoTracking()
                    .Where(l => l.LoteId == lote.LotePadreId.Value && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
                    .FirstOrDefaultAsync();

                if (lotePadre != null)
                {
                    if (string.IsNullOrEmpty(raza) && !string.IsNullOrEmpty(lotePadre.Raza))
                        raza = lotePadre.Raza;
                    if (!anoTablaGenetica.HasValue && lotePadre.AnoTablaGenetica.HasValue)
                        anoTablaGenetica = lotePadre.AnoTablaGenetica;
                }
            }

            return Ok(new
            {
                loteId = lote.LoteId,
                loteNombre = lote.LoteNombre,
                granjaId = lote.GranjaId,
                granjaNombre = lote.Farm?.Name,
                nucleoId = lote.NucleoId,
                nucleoNombre = lote.Nucleo?.NucleoNombre,
                galponId = lote.GalponId,
                galponNombre = lote.Galpon?.GalponNombre,
                etapa,
                tipoLote,
                lotePosturaLevanteId = ctx.LotePosturaLevante?.LotePosturaLevanteId,
                lotePosturaProduccionId = ctx.LotePosturaProduccion?.LotePosturaProduccionId,
                hembrasIniciales,
                machosIniciales,
                cantidadHembras = hembrasActuales,
                cantidadMachos = machosActuales,
                cantidadMixtas = mixtasActuales,
                totalAves = totalAvesActuales,
                fechaEncasetamiento = lote.FechaEncaset,
                fechaInicioProduccion,
                diasDesdeEncasetamiento = ctx.DiasDesdeEncaset,
                raza,
                anoTablaGenetica
            });
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
            var ctx = await ObtenerContextoLoteAsync(loteId);
            if (ctx == null)
                return NotFound(new { error = $"Lote {loteId} no encontrado" });

            var fechaNorm = fecha.Date;
            var loteIdStr = loteId.ToString();

            ZooSanMarino.Domain.Entities.SeguimientoDiario? registro = null;

            if (ctx.TipoLote == "Produccion")
            {
                // Para producción: buscar por LotePosturaProduccionId si existe, o por lote_id + tipo
                if (ctx.LotePosturaProduccion?.LotePosturaProduccionId is int lppId)
                {
                    registro = await _context.SeguimientoDiario
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s =>
                            s.LotePosturaProduccionId == lppId &&
                            s.TipoSeguimiento == "produccion" &&
                            s.Fecha.Date == fechaNorm);
                }

                registro ??= await _context.SeguimientoDiario
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.LoteId == loteIdStr &&
                        s.TipoSeguimiento == "produccion" &&
                        s.Fecha.Date == fechaNorm);
            }
            else
            {
                // Para levante: buscar por LotePosturaLevanteId si existe, o por lote_id + tipo
                if (ctx.LotePosturaLevante?.LotePosturaLevanteId is int lplId)
                {
                    registro = await _context.SeguimientoDiario
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s =>
                            s.LotePosturaLevanteId == lplId &&
                            s.TipoSeguimiento == "levante" &&
                            s.Fecha.Date == fechaNorm);
                }

                registro ??= await _context.SeguimientoDiario
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.LoteId == loteIdStr &&
                        s.TipoSeguimiento == "levante" &&
                        s.Fecha.Date == fechaNorm);
            }

            if (registro is null)
            {
                var fechaDisplay = fechaNorm.ToString("dd/MM/yyyy");
                return UnprocessableEntity(new
                {
                    existe = false,
                    error = $"No existe registro de Seguimiento Diario para el lote {loteId} en la fecha {fechaDisplay}. " +
                            "Cree el seguimiento del día antes de registrar el movimiento.",
                    loteId,
                    fecha = fechaNorm,
                    tipoLote = ctx.TipoLote
                });
            }

            return Ok(new
            {
                existe = true,
                seguimientoId = registro.Id,
                tipoSeguimiento = registro.TipoSeguimiento,
                lotePosturaLevanteId = registro.LotePosturaLevanteId,
                lotePosturaProduccionId = registro.LotePosturaProduccionId,
                fecha = registro.Fecha,
                trasladoAvesEntrante = registro.TrasladoAvesEntrante,
                trasladoAvesSalida = registro.TrasladoAvesSalida,
                ventaAvesCantidad = registro.VentaAvesCantidad,
                ventaAvesMotivo = registro.VentaAvesMotivo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar fecha de seguimiento para lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed record ContextoLote(
        ZooSanMarino.Domain.Entities.Lote Lote,
        int DiasDesdeEncaset,
        int Etapa,
        string TipoLote,
        ZooSanMarino.Domain.Entities.LotePosturaLevante? LotePosturaLevante,
        ZooSanMarino.Domain.Entities.LotePosturaProduccion? LotePosturaProduccion);

    private async Task<ContextoLote?> ObtenerContextoLoteAsync(int loteId)
    {
        var lote = await _context.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lote is null) return null;

        var diasDesdeEncaset = lote.FechaEncaset.HasValue
            ? (DateTime.UtcNow.Date - lote.FechaEncaset.Value.Date).Days
            : 0;
        var etapa = diasDesdeEncaset > 0 ? (diasDesdeEncaset / 7) + 1 : 0;

        var lotePosturaLev = await _context.LotePosturaLevante
            .AsNoTracking()
            .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .FirstOrDefaultAsync();

        var tipoLote = lote.Fase ?? "Levante";
        if (tipoLote == "Levante" && (lotePosturaLev?.EstadoCierre == "Cerrado" || etapa >= 26))
            tipoLote = "Produccion";

        ZooSanMarino.Domain.Entities.LotePosturaProduccion? lotePosturaProd = null;
        if (tipoLote == "Produccion")
        {
            lotePosturaProd = await _context.LotePosturaProduccion
                .AsNoTracking()
                .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
                .FirstOrDefaultAsync();
        }

        return new ContextoLote(lote, diasDesdeEncaset, etapa, tipoLote, lotePosturaLev, lotePosturaProd);
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
