// src/ZooSanMarino.API/Controllers/TrasladosController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/traslados")]
[Authorize]
[Produces("application/json")]
public class TrasladosController : ControllerBase
{
    private readonly IDisponibilidadLoteService _disponibilidadService;
    private readonly ITrasladoHuevosService _trasladoHuevosService;
    private readonly IMovimientoAvesService _movimientoAvesService;
    private readonly ICurrentUser _currentUser;

    public TrasladosController(
        IDisponibilidadLoteService disponibilidadService,
        ITrasladoHuevosService trasladoHuevosService,
        IMovimientoAvesService movimientoAvesService,
        ICurrentUser currentUser)
    {
        _disponibilidadService = disponibilidadService;
        _trasladoHuevosService = trasladoHuevosService;
        _movimientoAvesService = movimientoAvesService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Obtiene la información completa de disponibilidad de un lote
    /// </summary>
    [HttpGet("lote/{loteId}/disponibilidad")]
    [ProducesResponseType(typeof(DisponibilidadLoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DisponibilidadLoteDto>> ObtenerDisponibilidadLote(string loteId)
    {
        var disponibilidad = await _disponibilidadService.ObtenerDisponibilidadLoteAsync(loteId);
        
        if (disponibilidad == null)
        {
            return NotFound(new { message = $"Lote {loteId} no encontrado" });
        }

        return Ok(disponibilidad);
    }

    /// <summary>
    /// Crea un traslado de aves (venta o traslado)
    /// </summary>
    [HttpPost("aves")]
    [ProducesResponseType(typeof(MovimientoAvesDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MovimientoAvesDto>> CrearTrasladoAves([FromBody] CrearTrasladoAvesDto dto)
    {
        try
        {
            // Validar disponibilidad
            var hayDisponibilidad = await _disponibilidadService.ValidarDisponibilidadAvesAsync(
                dto.LoteId, 
                dto.CantidadHembras, 
                dto.CantidadMachos);

            if (!hayDisponibilidad)
            {
                return BadRequest(new { message = "No hay suficientes aves disponibles para este traslado" });
            }

            // Obtener información del lote para determinar granja origen
            var loteIdInt = int.TryParse(dto.LoteId, out var loteIdParsed) ? loteIdParsed : 0;
            var disponibilidad = await _disponibilidadService.ObtenerDisponibilidadLoteAsync(dto.LoteId);
            if (disponibilidad == null)
            {
                return BadRequest(new { message = $"Lote {dto.LoteId} no encontrado" });
            }

            // Convertir a CreateMovimientoAvesDto
            var movimientoDto = new CreateMovimientoAvesDto
            {
                FechaMovimiento = dto.FechaTraslado,
                TipoMovimiento = dto.TipoOperacion,
                LoteOrigenId = loteIdInt,
                GranjaOrigenId = disponibilidad.GranjaId,
                CantidadHembras = dto.CantidadHembras,
                CantidadMachos = dto.CantidadMachos,
                CantidadMixtas = 0,
                GranjaDestinoId = dto.GranjaDestinoId,
                LoteDestinoId = dto.LoteDestinoId != null && int.TryParse(dto.LoteDestinoId, out var loteDestId) ? loteDestId : null,
                MotivoMovimiento = dto.Motivo,
                Observaciones = $"{dto.Descripcion ?? string.Empty} | {dto.Observaciones ?? string.Empty}".Trim('|', ' '),
                UsuarioMovimientoId = _currentUser.UserId
            };

            var movimiento = await _movimientoAvesService.CreateAsync(movimientoDto);
            
            // Procesar automáticamente el movimiento
            var procesarDto = new ProcesarMovimientoDto
            {
                MovimientoId = movimiento.Id,
                AutoCrearInventarioDestino = true
            };
            await _movimientoAvesService.ProcesarMovimientoAsync(procesarDto);

            return CreatedAtAction(nameof(GetMovimientoAves), new { id = movimiento.Id }, movimiento);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Crea un traslado de huevos (venta o traslado)
    /// </summary>
    [HttpPost("huevos")]
    [ProducesResponseType(typeof(TrasladoHuevosDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TrasladoHuevosDto>> CrearTrasladoHuevos([FromBody] CrearTrasladoHuevosDto dto)
    {
        try
        {
            var traslado = await _trasladoHuevosService.CrearTrasladoHuevosAsync(dto, _currentUser.UserId);
            return CreatedAtAction(nameof(GetTrasladoHuevos), new { id = traslado.Id }, traslado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error interno del servidor", details = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene un movimiento de aves por ID
    /// </summary>
    [HttpGet("aves/{id}")]
    [ProducesResponseType(typeof(MovimientoAvesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimientoAvesDto>> GetMovimientoAves(int id)
    {
        var movimiento = await _movimientoAvesService.GetByIdAsync(id);
        
        if (movimiento == null)
        {
            return NotFound();
        }

        return Ok(movimiento);
    }

    /// <summary>
    /// Obtiene un traslado de huevos por ID
    /// </summary>
    [HttpGet("huevos/{id}")]
    [ProducesResponseType(typeof(TrasladoHuevosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrasladoHuevosDto>> GetTrasladoHuevos(int id)
    {
        var traslados = await _trasladoHuevosService.ObtenerTrasladosPorLoteAsync(id.ToString());
        var traslado = traslados.FirstOrDefault(t => t.Id == id);
        
        if (traslado == null)
        {
            return NotFound();
        }

        return Ok(traslado);
    }

    /// <summary>
    /// Obtiene todos los traslados de huevos de un lote
    /// </summary>
    [HttpGet("huevos/lote/{loteId}")]
    [ProducesResponseType(typeof(IEnumerable<TrasladoHuevosDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TrasladoHuevosDto>>> GetTrasladosHuevosPorLote(string loteId)
    {
        var traslados = await _trasladoHuevosService.ObtenerTrasladosPorLoteAsync(loteId);
        return Ok(traslados);
    }
}

