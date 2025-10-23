// src/ZooSanMarino.API/Controllers/LiquidacionTecnicaController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LiquidacionTecnicaController : ControllerBase
{
    private readonly ILiquidacionTecnicaService _liquidacionService;
    private readonly ILogger<LiquidacionTecnicaController> _logger;

    public LiquidacionTecnicaController(
        ILiquidacionTecnicaService liquidacionService,
        ILogger<LiquidacionTecnicaController> logger)
    {
        _liquidacionService = liquidacionService;
        _logger = logger;
    }

    /// <summary>
    /// Calcula la liquidación técnica de un lote de levante hasta la semana 25
    /// </summary>
    /// <param name="loteId">ID del lote</param>
    /// <param name="fechaHasta">Fecha límite para el cálculo (opcional)</param>
    /// <returns>Resultados de la liquidación técnica</returns>
    [HttpGet("{loteId}")]
    public async Task<ActionResult<LiquidacionTecnicaDto>> CalcularLiquidacion(
        int loteId,
        [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            var request = new LiquidacionTecnicaRequest(loteId, fechaHasta);
            var resultado = await _liquidacionService.CalcularLiquidacionAsync(request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Error al calcular liquidación para lote {LoteId}: {Error}", loteId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interno al calcular liquidación para lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene la liquidación técnica completa con detalles del seguimiento diario
    /// </summary>
    /// <param name="loteId">ID del lote</param>
    /// <param name="fechaHasta">Fecha límite para el cálculo (opcional)</param>
    /// <returns>Liquidación completa con detalles</returns>
    [HttpGet("{loteId}/completa")]
    public async Task<ActionResult<LiquidacionTecnicaCompletaDto>> ObtenerLiquidacionCompleta(
        int loteId,
        [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            var request = new LiquidacionTecnicaRequest(loteId, fechaHasta);
            var resultado = await _liquidacionService.ObtenerLiquidacionCompletaAsync(request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Error al obtener liquidación completa para lote {LoteId}: {Error}", loteId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interno al obtener liquidación completa para lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Calcula la liquidación técnica usando datos del cuerpo de la petición
    /// </summary>
    /// <param name="request">Datos de la solicitud</param>
    /// <returns>Resultados de la liquidación técnica</returns>
    [HttpPost("calcular")]
    public async Task<ActionResult<LiquidacionTecnicaDto>> CalcularLiquidacionPost(
        [FromBody] LiquidacionTecnicaRequest request)
    {
        try
        {
            var resultado = await _liquidacionService.CalcularLiquidacionAsync(request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Error al calcular liquidación para lote {LoteId}: {Error}", request.LoteId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interno al calcular liquidación para lote {LoteId}", request.LoteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Valida si un lote puede ser procesado para liquidación técnica
    /// </summary>
    /// <param name="loteId">ID del lote</param>
    /// <returns>True si el lote es válido para liquidación</returns>
    [HttpGet("{loteId}/validar")]
    public async Task<ActionResult<bool>> ValidarLote(int loteId)
    {
        try
        {
            var esValido = await _liquidacionService.ValidarLoteParaLiquidacionAsync(loteId);
            return Ok(new { loteId, esValido, mensaje = esValido ? "Lote válido para liquidación" : "Lote no válido o sin datos de seguimiento" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Endpoint de debug para verificar datos del lote y seguimientos
    /// </summary>
    /// <param name="loteId">ID del lote</param>
    /// <returns>Información de debug</returns>
    [HttpGet("{loteId}/debug")]
    public async Task<ActionResult> DebugLote(int loteId)
    {
        try
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ZooSanMarino.Infrastructure.Persistence.ZooSanMarinoContext>();
            
            // Verificar si el lote existe
            var lote = await context.Lotes
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == loteId);
            
            if (lote == null)
            {
                return Ok(new { 
                    loteId, 
                    loteExiste = false, 
                    mensaje = "Lote no encontrado" 
                });
            }
            
            // Verificar seguimientos
            var seguimientos = await context.SeguimientoLoteLevante
                .AsNoTracking()
                .Where(s => s.LoteId == loteId)
                .ToListAsync();
            
            return Ok(new { 
                loteId, 
                loteExiste = true,
                loteNombre = lote.LoteNombre,
                fechaEncaset = lote.FechaEncaset,
                raza = lote.Raza,
                anoTablaGenetica = lote.AnoTablaGenetica,
                totalSeguimientos = seguimientos.Count,
                seguimientos = seguimientos.Take(5).Select(s => new {
                    s.Id,
                    s.FechaRegistro,
                    s.MortalidadHembras,
                    s.MortalidadMachos,
                    s.ConsumoKgHembras,
                    s.PesoPromH,
                    s.PesoPromM
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en debug para lote {LoteId}", loteId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene un resumen de múltiples lotes para liquidación
    /// </summary>
    /// <param name="loteIds">Lista de IDs de lotes</param>
    /// <returns>Resumen de validación de lotes</returns>
    [HttpPost("validar-multiples")]
    public async Task<ActionResult> ValidarMultiplesLotes([FromBody] List<int> loteIds)
    {
        try
        {
            var resultados = new List<object>();

            foreach (var loteId in loteIds)
            {
                var esValido = await _liquidacionService.ValidarLoteParaLiquidacionAsync(loteId);
                resultados.Add(new { loteId, esValido });
            }

            return Ok(new { 
                total = loteIds.Count,
                validos = resultados.Count(r => (bool)r.GetType().GetProperty("esValido")!.GetValue(r)!),
                resultados 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar múltiples lotes");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }
}
