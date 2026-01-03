// src/ZooSanMarino.API/Controllers/CatalogoAlimentosController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/catalogo-alimentos")]
[Tags("Catalogo")]
public class CatalogoAlimentosController : ControllerBase
{
    private readonly ICatalogItemService _service;

    public CatalogoAlimentosController(ICatalogItemService service)
    {
        _service = service;
    }

    // ✅ Lista completa (sin paginación)
    // GET /api/catalogo-alimentos/old
    // GET /catalogo-alimentos/old (alias sin /api)
    [HttpGet("old")]
    [HttpGet("/catalogo-alimentos/old")]
    [ProducesResponseType(typeof(IEnumerable<CatalogItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? q = null, CancellationToken ct = default)
    {
        var items = await _service.GetAllAsync(q, ct);
        return Ok(items);
    }

    // ✅ Lista paginada (lo que consume Angular)
    // GET /api/catalogo-alimentos
    // GET /catalogo-alimentos (alias sin /api)
    [HttpGet]
    [HttpGet("/catalogo-alimentos")]
    [ProducesResponseType(typeof(PagedResult<CatalogItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetAsync(q, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Obtiene un item por Id.</summary>
    // GET /api/catalogo-alimentos/{id}
    // GET /catalogo-alimentos/{id} (alias sin /api)
    [HttpGet("{id:int}")]
    [HttpGet("/catalogo-alimentos/{id:int}")]
    [ProducesResponseType(typeof(CatalogItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct = default)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Crea un nuevo item.</summary>
    // POST /api/catalogo-alimentos
    // POST /catalogo-alimentos (alias sin /api)
    [HttpPost]
    [HttpPost("/catalogo-alimentos")]
    [ProducesResponseType(typeof(CatalogItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CatalogItemCreateRequest req, CancellationToken ct = default)
    {
        var created = await _service.CreateAsync(req, ct);
        return created is null
            ? Conflict(new { message = "Código ya existe." })
            : CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Actualiza un item existente.</summary>
    // PUT /api/catalogo-alimentos/{id}
    // PUT /catalogo-alimentos/{id} (alias sin /api)
    [HttpPut("{id:int}")]
    [HttpPut("/catalogo-alimentos/{id:int}")]
    [ProducesResponseType(typeof(CatalogItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromRoute] int id,
        [FromBody] CatalogItemUpdateRequest req,
        CancellationToken ct = default)
    {
        var updated = await _service.UpdateAsync(id, req, ct);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    /// <summary>Elimina un item. Por defecto borrado lógico (activo=false). Usa ?hard=true para borrado físico.</summary>
    // DELETE /api/catalogo-alimentos/{id}
    // DELETE /catalogo-alimentos/{id} (alias sin /api)
    [HttpDelete("{id:int}")]
    [HttpDelete("/catalogo-alimentos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] int id,
        [FromQuery] bool hard = false,
        CancellationToken ct = default)
    {
        var ok = await _service.DeleteAsync(id, hard, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("metadata")]
    [HttpGet("/catalogo-alimentos/metadata")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetMetadata()
    {
        var metadata = new
        {
            Campos = new[]
            {
                new { Nombre = "Id", Tipo = "int", Requerido = true, Descripcion = "Identificador único" },
                new { Nombre = "Codigo", Tipo = "string", Requerido = true, Descripcion = "Código único del alimento" },
                new { Nombre = "Nombre", Tipo = "string", Requerido = true, Descripcion = "Nombre del alimento" },
                new { Nombre = "Metadata", Tipo = "object", Requerido = false, Descripcion = "Datos adicionales en formato JSON" },
                new { Nombre = "Activo", Tipo = "bool", Requerido = true, Descripcion = "Indica si el alimento está activo" }
            }
        };
        return Ok(metadata);
    }

    /// <summary>Obtiene productos filtrados por tipo de item y búsqueda, solo activos.</summary>
    /// <param name="typeItem">Tipo de item a filtrar (alimento, medicamento, etc.). Si es null o vacío, trae todos los tipos.</param>
    /// <param name="search">Texto de búsqueda para filtrar por código o nombre.</param>
    /// <returns>Lista de productos activos que coinciden con los filtros.</returns>
    // GET /api/catalogo-alimentos/filter
    // GET /catalogo-alimentos/filter (alias sin /api)
    [HttpGet("filter")]
    [HttpGet("/catalogo-alimentos/filter")]
    [ProducesResponseType(typeof(IEnumerable<CatalogItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByType(
        [FromQuery] string? typeItem = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var items = await _service.GetByTypeAsync(typeItem, search, ct);
        return Ok(items);
    }
}
