// src/ZooSanMarino.API/Controllers/ProduccionAvicolaRawController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("ProduccionAvicolaRaw")]
public class ProduccionAvicolaRawController : ControllerBase
{
    private readonly IProduccionAvicolaRawService _service;
    private readonly ILogger<ProduccionAvicolaRawController> _logger;

    public ProduccionAvicolaRawController(
        IProduccionAvicolaRawService service, 
        ILogger<ProduccionAvicolaRawController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Obtiene todos los registros de producción avícola.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProduccionAvicolaRawDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProduccionAvicolaRawDto>>> GetAll()
    {
        try
        {
            var items = await _service.GetAllAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener todos los registros de producción avícola");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "Error interno del servidor" });
        }
    }

    /// <summary>Obtiene un registro de producción avícola por ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProduccionAvicolaRawDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProduccionAvicolaRawDto>> GetById(int id)
    {
        try
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null)
                return NotFound(new { message = $"Registro con ID {id} no encontrado" });

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener registro de producción avícola con ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "Error interno del servidor" });
        }
    }

    /// <summary>Busca registros de producción avícola con filtros y paginación.</summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(ZooSanMarino.Application.DTOs.Common.PagedResult<ProduccionAvicolaRawDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ZooSanMarino.Application.DTOs.Common.PagedResult<ProduccionAvicolaRawDto>>> Search([FromBody] ProduccionAvicolaRawSearchRequest request)
    {
        try
        {
            var result = await _service.SearchAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar registros de producción avícola");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "Error interno del servidor" });
        }
    }

    /// <summary>Obtiene opciones de filtros (Año guía y Raza) según datos cargados.</summary>
    [HttpGet("filters")]
    [ProducesResponseType(typeof(ProduccionAvicolaRawFilterOptionsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProduccionAvicolaRawFilterOptionsDto>> GetFilters()
    {
        try
        {
            var filters = await _service.GetFilterOptionsAsync();
            return Ok(filters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener filtros de producción avícola raw");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Error interno del servidor" });
        }
    }

    /// <summary>Crea un nuevo registro de producción avícola.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProduccionAvicolaRawDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProduccionAvicolaRawDto>> Create([FromBody] CreateProduccionAvicolaRawDto dto)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (dto == null)
            return BadRequest(new { message = "El cuerpo de la petición es requerido" });

        try
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear registro de producción avícola");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "Error interno del servidor" });
        }
    }

    /// <summary>Actualiza un registro de producción avícola existente.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProduccionAvicolaRawDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProduccionAvicolaRawDto>> Update(int id, [FromBody] UpdateProduccionAvicolaRawDto dto)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (dto == null)
            return BadRequest(new { message = "El cuerpo de la petición es requerido" });

        if (id != dto.Id)
            return BadRequest(new { message = "El ID de la ruta no coincide con el del cuerpo" });

        try
        {
            var updated = await _service.UpdateAsync(dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Registro con ID {id} no encontrado" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar registro de producción avícola con ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "Error interno del servidor" });
        }
    }

    /// <summary>Elimina un registro de producción avícola.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(new { message = $"Registro con ID {id} no encontrado" });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar registro de producción avícola con ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "Error interno del servidor" });
        }
    }
}
