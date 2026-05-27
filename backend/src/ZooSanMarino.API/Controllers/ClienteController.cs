using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Cliente;
using ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/clientes")]
[Produces("application/json")]
public class ClienteController : ControllerBase
{
    private readonly IClienteService _service;

    public ClienteController(IClienteService service) => _service = service;

    /// <summary>Listado completo de clientes activos</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ClienteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ClienteDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    /// <summary>Búsqueda paginada con filtros</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<ClienteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ClienteDto>>> Search(
        [FromQuery] string?  search        = null,
        [FromQuery] string?  tipoCliente   = null,
        [FromQuery] string?  pais          = null,
        [FromQuery] string?  tipoDocumento = null,
        [FromQuery] string?  zona          = null,
        [FromQuery] bool     soloActivos   = true,
        [FromQuery] string   sortBy        = "nombre",
        [FromQuery] bool     sortDesc      = false,
        [FromQuery] int      page          = 1,
        [FromQuery] int      pageSize      = 20,
        CancellationToken ct = default)
    {
        var req = new ClienteSearchRequest(search, tipoCliente, pais, tipoDocumento, zona, soloActivos, sortBy, sortDesc, page, pageSize);
        return Ok(await _service.SearchAsync(req, ct));
    }

    /// <summary>Obtiene un cliente por ID</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ClienteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteDto>> GetById(int id, CancellationToken ct)
    {
        var dto = await _service.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Crea un nuevo cliente</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClienteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClienteDto>> Create([FromBody] CreateClienteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TipoDocumento) ||
            string.IsNullOrWhiteSpace(req.NumeroIdentificacion) ||
            string.IsNullOrWhiteSpace(req.Nombre))
            return BadRequest("TipoDocumento, NumeroIdentificacion y Nombre son requeridos.");

        var dto = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    /// <summary>Actualiza un cliente existente</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ClienteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClienteDto>> Update(int id, [FromBody] UpdateClienteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TipoDocumento) ||
            string.IsNullOrWhiteSpace(req.NumeroIdentificacion) ||
            string.IsNullOrWhiteSpace(req.Nombre))
            return BadRequest("TipoDocumento, NumeroIdentificacion y Nombre son requeridos.");

        var dto = await _service.UpdateAsync(id, req, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Eliminación lógica de un cliente</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
