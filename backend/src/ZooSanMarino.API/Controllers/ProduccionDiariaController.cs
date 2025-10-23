using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProduccionDiariaController : ControllerBase
{
    private readonly IProduccionDiariaService _svc;

    public ProduccionDiariaController(IProduccionDiariaService svc)
    {
        _svc = svc;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProduccionDiariaDto dto)
    {
        try
        {
            var result = await _svc.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByLoteId), new { loteId = dto.LoteId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{loteId}")]
    public async Task<IActionResult> GetByLoteId(string loteId)
    {
        try
        {
            var loteIdInt = int.Parse(loteId);
            var result = await _svc.GetByLoteIdAsync(loteIdInt);
            return Ok(result);
        }
        catch (FormatException)
        {
            return BadRequest(new { message = "LoteId debe ser un número válido" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _svc.GetAllAsync();
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateProduccionDiariaDto dto)
    {
        if (id != dto.Id)
            return BadRequest(new { message = "El ID en la URL no coincide con el ID en el cuerpo de la petición" });

        try
        {
            var result = await _svc.UpdateAsync(dto);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _svc.DeleteAsync(id);
        return result ? NoContent() : NotFound();
    }

    [HttpPost("filter")]
    public async Task<IActionResult> Filter(FilterProduccionDiariaDto filter)
    {
        var result = await _svc.FilterAsync(filter);
        return Ok(result);
    }

    [HttpGet("check-config/{loteId}")]
    public async Task<IActionResult> CheckProduccionLoteConfig(string loteId)
    {
        try
        {
            var hasConfig = await _svc.HasProduccionLoteConfigAsync(loteId);
            return Ok(new { hasProduccionLoteConfig = hasConfig });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
