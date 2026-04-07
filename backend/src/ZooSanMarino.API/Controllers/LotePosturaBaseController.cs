using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LotePosturaBaseController : ControllerBase
{
    private readonly ILotePosturaBaseService _svc;
    public LotePosturaBaseController(ILotePosturaBaseService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LotePosturaBaseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LotePosturaBaseDto>>> GetAll()
    {
        var items = await _svc.GetAllAsync();
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LotePosturaBaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotePosturaBaseDto>> GetById(int id)
    {
        var dto = await _svc.GetByIdAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [ProducesResponseType(typeof(LotePosturaBaseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LotePosturaBaseDto>> Create([FromBody] CreateLotePosturaBaseDto dto)
    {
        if (dto is null) return BadRequest(new { message = "El cuerpo de la petición es requerido." });
        try
        {
            var created = await _svc.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.LotePosturaBaseId }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

