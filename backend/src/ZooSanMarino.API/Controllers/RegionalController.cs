// src/ZooSanMarino.API/Controllers/RegionalController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegionalController : ControllerBase
{
    private readonly IRegionalService _svc;
    public RegionalController(IRegionalService svc) => _svc = svc;

    [HttpGet] public async Task<IActionResult> GetAll() => Ok(await _svc.GetAllAsync());
    [HttpGet("{cia}/{id}")]
    public async Task<IActionResult> GetById(int cia, int id) =>
        (await _svc.GetByIdAsync(cia, id)) is RegionalDto dto ? Ok(dto) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create(CreateRegionalDto dto)
    {
        var cr = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { cia = cr.RegionalCia, id = cr.RegionalId }, cr);
    }

    [HttpPut("{cia}/{id}")]
    public async Task<IActionResult> Update(int cia, int id, UpdateRegionalDto dto)
    {
        if (dto.RegionalCia != cia || dto.RegionalId != id) return BadRequest();
        return (await _svc.UpdateAsync(dto)) is RegionalDto upd ? Ok(upd) : NotFound();
    }

    [HttpDelete("{cia}/{id}")]
    public async Task<IActionResult> Delete(int cia, int id) =>
        (await _svc.DeleteAsync(cia, id)) ? NoContent() : NotFound();
        
        
}
