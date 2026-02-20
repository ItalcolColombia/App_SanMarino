// src/ZooSanMarino.API/Controllers/MasterListController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MasterListController : ControllerBase
{
    private readonly IMasterListService _svc;
    public MasterListController(IMasterListService svc) => _svc = svc;

    // Listar todas (filtradas por CompanyId y CountryId)
    [HttpGet] 
    public async Task<IActionResult> GetAll(
        [FromQuery] int? companyId = null,
        [FromQuery] int? countryId = null) => 
        Ok(await _svc.GetAllAsync(companyId, countryId));

    // Obtener por id
    [HttpGet("{id:int}")] 
    public async Task<IActionResult> GetById(int id) =>
        (await _svc.GetByIdAsync(id)) is MasterListDto dto ? Ok(dto) : NotFound();

    // Obtener por clave (filtrado por CompanyId y CountryId)
    [HttpGet("byKey/{key}")] 
    public async Task<IActionResult> GetByKey(
        string key,
        [FromQuery] int? companyId = null,
        [FromQuery] int? countryId = null) =>
        (await _svc.GetByKeyAsync(key, companyId, countryId)) is MasterListDto dto ? Ok(dto) : NotFound();

    // Crear
    [HttpPost] public async Task<IActionResult> Create(CreateMasterListDto dto)
    {
        var cr = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = cr.Id }, cr);
    }
    // Actualizar
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, UpdateMasterListDto dto)
    {
        if (dto.Id != id) return BadRequest();
        return (await _svc.UpdateAsync(dto)) is MasterListDto upd
            ? Ok(upd) : NotFound();
    }

    // Borrar
    [HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id) =>
        (await _svc.DeleteAsync(id)) ? NoContent() : NotFound();
}
