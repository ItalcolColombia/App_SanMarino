// src/ZooSanMarino.API/Controllers/MunicipioController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MunicipioController : ControllerBase
{
    private readonly IMunicipioService _svc;
    public MunicipioController(IMunicipioService svc) => _svc = svc;

    // GET api/Municipio?departamentoId=###
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? departamentoId)
        => departamentoId.HasValue
            ? Ok(await _svc.GetByDepartamentoIdAsync(departamentoId.Value))
            : Ok(await _svc.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => (await _svc.GetByIdAsync(id)) is MunicipioDto dto ? Ok(dto) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create(CreateMunicipioDto dto)
    {
        var created = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.MunicipioId }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateMunicipioDto dto)
    {
        if (id != dto.MunicipioId) return BadRequest();
        return await _svc.UpdateAsync(dto) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => await _svc.DeleteAsync(id) ? NoContent() : NotFound();
}
