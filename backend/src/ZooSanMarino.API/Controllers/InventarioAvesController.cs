// src/ZooSanMarino.API/Controllers/InventarioAvesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Controlador para gestión de inventario de aves
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Tags("Inventario de Aves")]
public class InventarioAvesController : ControllerBase
{
    private readonly IInventarioAvesService _inventarioService;
    private readonly ILogger<InventarioAvesController> _logger;

    public InventarioAvesController(
        IInventarioAvesService inventarioService,
        ILogger<InventarioAvesController> logger)
    {
        _inventarioService = inventarioService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los inventarios activos
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<InventarioAvesDto>))]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var inventarios = await _inventarioService.GetInventariosActivosAsync();
            return Ok(inventarios);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener inventarios");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Busca inventarios con filtros y paginación
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ZooSanMarino.Application.DTOs.Common.PagedResult<InventarioAvesDto>))]
    public async Task<IActionResult> Search([FromBody] InventarioAvesSearchRequest request)
    {
        try
        {
            var result = await _inventarioService.SearchAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar inventarios");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene un inventario por ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InventarioAvesDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var inventario = await _inventarioService.GetByIdAsync(id);
            if (inventario == null)
                return NotFound(new { error = $"Inventario con ID {id} no encontrado" });

            return Ok(inventario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener inventario {Id}", id);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene inventarios por lote
    /// </summary>
    [HttpGet("lote/{loteId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<InventarioAvesDto>))]
    public async Task<IActionResult> GetByLote(int loteId)  // Changed from string to int
    {
        try
        {
            var inventarios = await _inventarioService.GetByLoteIdAsync(loteId);  // Changed from loteId
            return Ok(inventarios);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener inventarios del lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el estado completo de un lote
    /// </summary>
    [HttpGet("lote/{loteId}/estado")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(EstadoLoteDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEstadoLote(int loteId)  // Changed from string to int
    {
        try
        {
            var estado = await _inventarioService.GetEstadoLoteAsync(loteId);  // Changed from loteId
            return Ok(estado);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener estado del lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene inventarios por ubicación
    /// </summary>
    [HttpGet("ubicacion")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<InventarioAvesDto>))]
    public async Task<IActionResult> GetByUbicacion(
        [FromQuery] int granjaId,
        [FromQuery] string? nucleoId = null,
        [FromQuery] string? galponId = null)
    {
        try
        {
            var inventarios = await _inventarioService.GetByUbicacionAsync(granjaId, nucleoId, galponId);
            return Ok(inventarios);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener inventarios por ubicación");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene resumen de inventarios por ubicación
    /// </summary>
    [HttpGet("resumen")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<ResumenInventarioDto>))]
    public async Task<IActionResult> GetResumen([FromQuery] int? granjaId = null)
    {
        try
        {
            var resumen = await _inventarioService.GetResumenPorUbicacionAsync(granjaId);
            return Ok(resumen);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener resumen de inventarios");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Crea un nuevo inventario
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(InventarioAvesDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateInventarioAvesDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var inventario = await _inventarioService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = inventario.Id }, inventario);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear inventario");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Actualiza un inventario existente
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InventarioAvesDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateInventarioAvesDto dto)
    {
        try
        {
            if (id != dto.Id)
                return BadRequest(new { error = "El ID del parámetro no coincide con el ID del cuerpo" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var inventario = await _inventarioService.UpdateAsync(dto);
            return Ok(inventario);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar inventario {Id}", id);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Elimina un inventario
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _inventarioService.DeleteAsync(id);
            if (!deleted)
                return NotFound(new { error = $"Inventario con ID {id} no encontrado" });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar inventario {Id}", id);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Ajusta las cantidades de un inventario
    /// </summary>
    [HttpPost("{id}/ajustar")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InventarioAvesDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AjustarInventario(int id, [FromBody] AjustarInventarioRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var inventario = await _inventarioService.AjustarInventarioAsync(
                id, request.CantidadHembras, request.CantidadMachos, request.CantidadMixtas,
                request.Motivo, request.Observaciones);

            return Ok(inventario);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ajustar inventario {Id}", id);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Traslada un inventario a otra ubicación
    /// </summary>
    [HttpPost("{id}/trasladar")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResultadoMovimientoDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TrasladarInventario(int id, [FromBody] TrasladarInventarioRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var resultado = await _inventarioService.TrasladarInventarioAsync(
                id, request.GranjaDestinoId, request.NucleoDestinoId, request.GalponDestinoId, request.Motivo);

            if (!resultado.Success)
                return BadRequest(resultado);

            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al trasladar inventario {Id}", id);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Inicializa inventario desde un lote existente
    /// </summary>
    [HttpPost("inicializar/{loteId}")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(InventarioAvesDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InicializarDesdeLotel(int loteId)  // Changed from string to int
    {
        try
        {
            var inventario = await _inventarioService.InicializarDesdeLotelAsync(loteId);  // Changed from loteId
            return CreatedAtAction(nameof(GetById), new { id = inventario.Id }, inventario);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inicializar inventario desde lote {LoteId}", loteId);
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Sincroniza inventarios desde lotes existentes
    /// </summary>
    [HttpPost("sincronizar")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<InventarioAvesDto>))]
    public async Task<IActionResult> SincronizarInventarios()
    {
        try
        {
            var inventarios = await _inventarioService.SincronizarInventariosAsync();
            return Ok(new { 
                message = $"Se sincronizaron {inventarios.Count()} inventarios",
                inventarios = inventarios
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al sincronizar inventarios");
            return StatusCode(500, new { error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Endpoint de debug para verificar datos en la BD
    /// </summary>
    [HttpGet("debug/count")]
    public async Task<IActionResult> GetDebugCount()
    {
        try
        {
            // Consulta directa sin filtros para debug
            var totalCount = await _inventarioService.GetTotalCountAsync();
            return Ok(new { 
                totalCount = totalCount,
                message = "Debug: Total de registros en inventario_aves"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en debug count");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

/// <summary>
/// Request para ajustar inventario
/// </summary>
public sealed class AjustarInventarioRequest
{
    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public int CantidadMixtas { get; set; }
    public string Motivo { get; set; } = null!;
    public string? Observaciones { get; set; }
}

/// <summary>
/// Request para trasladar inventario
/// </summary>
public sealed class TrasladarInventarioRequest
{
    public int GranjaDestinoId { get; set; }
    public string? NucleoDestinoId { get; set; }
    public string? GalponDestinoId { get; set; }
    public string? Motivo { get; set; }
}
