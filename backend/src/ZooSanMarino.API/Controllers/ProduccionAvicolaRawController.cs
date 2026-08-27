// src/ZooSanMarino.API/Controllers/ProduccionAvicolaRawController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.API.Infrastructure;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Guía Genética <b>Sanmarino</b> — la tabla ANCHA compartida
/// (<c>guia_genetica_sanmarino_colombia</c> / <c>ProduccionAvicolaRaw</c>), ~50 columnas,
/// reproductora + postura de Sanmarino, Demo, Ecuador y Panamá.
///
/// <para>
/// 🔴 <b>Guard fail-closed en las ESCRITURAS:</b> una empresa cuyo
/// <c>companies.guia_genetica_perfil</c> sea <c>reducida</c> administra su guía en el módulo de la
/// tabla plana y acá recibe <b>403</b>. Jamás se cae al otro perfil.
/// </para>
///
/// <para>
/// <b>Delta cero para quien escribe hoy:</b> sólo pasa a <c>reducida</c> la empresa que tiene filas
/// en <c>guia_genetica_santa_reyes</c> (backfill por datos de la migración
/// <c>AddGuiaGeneticaPerfilCompany</c>). Medido en la copia de producción el 26-ago-2026: es
/// <b>una sola</b> —Santa Reyes, id 6, con 615 filas propias y <b>0</b> en esta tabla—, así que
/// ninguna empresa que hoy escriba acá queda bloqueada. Empresa sin resolver o columna vacía ⇒
/// default <c>sanmarino</c> ⇒ pasa, como siempre.
/// </para>
///
/// <para>
/// Las LECTURAS quedan exactamente como estaban. Tampoco se le exige acá el permiso
/// <c>guia_genetica.gestionar</c>: eso cambiaría el comportamiento de cuatro empresas que hoy
/// escriben sin permiso alguno, y este trabajo tiene delta cero fuera de Santa Reyes por regla.
/// </para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("ProduccionAvicolaRaw")]
[Authorize]
public class ProduccionAvicolaRawController : ControllerBase
{
    private readonly IProduccionAvicolaRawService _service;
    private readonly IGuiaGeneticaPerfilResolver _perfilResolver;
    private readonly ILogger<ProduccionAvicolaRawController> _logger;

    public ProduccionAvicolaRawController(
        IProduccionAvicolaRawService service,
        IGuiaGeneticaPerfilResolver perfilResolver,
        ILogger<ProduccionAvicolaRawController> logger)
    {
        _service = service;
        _perfilResolver = perfilResolver;
        _logger = logger;
    }

    /// <summary>Perfil de guía que administra esta tabla.</summary>
    private const string PerfilDeLaTabla = GuiaGeneticaPerfilCalculos.Sanmarino;

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProduccionAvicolaRawDto>> Create([FromBody] CreateProduccionAvicolaRawDto dto)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (dto == null)
            return BadRequest(new { message = "El cuerpo de la petición es requerido" });

        var rechazo = await this.ExigirPerfilGuiaGeneticaAsync(_perfilResolver, PerfilDeLaTabla);
        if (rechazo is not null) return rechazo;

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

        var rechazo = await this.ExigirPerfilGuiaGeneticaAsync(_perfilResolver, PerfilDeLaTabla);
        if (rechazo is not null) return rechazo;

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var rechazo = await this.ExigirPerfilGuiaGeneticaAsync(_perfilResolver, PerfilDeLaTabla);
        if (rechazo is not null) return rechazo;

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
