// src/ZooSanMarino.API/Controllers/DepartamentoController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DepartamentoController : ControllerBase
{
    private readonly IDepartamentoService _svc;
    public DepartamentoController(IDepartamentoService svc) => _svc = svc;

    // GET api/Departamento?paisId=###
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? paisId)
        => paisId.HasValue
            ? Ok(await _svc.GetByPaisIdAsync(paisId.Value))
            : Ok(await _svc.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => (await _svc.GetByIdAsync(id)) is DepartamentoDto dto ? Ok(dto) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create(CreateDepartamentoDto dto)
    {
        var created = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.DepartamentoId }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateDepartamentoDto dto)
    {
        if (id != dto.DepartamentoId) return BadRequest();
        return await _svc.UpdateAsync(dto) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => await _svc.DeleteAsync(id) ? NoContent() : NotFound();
}
