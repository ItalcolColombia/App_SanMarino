// src/ZooSanMarino.API/Controllers/PaisController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaisController : ControllerBase
{
    private readonly IPaisService _svc;
    private readonly IUserPermissionService _userPermissionService;
    private readonly ICurrentUser _currentUser;
    
    public PaisController(IPaisService svc, IUserPermissionService userPermissionService, ICurrentUser currentUser)
    {
        _svc = svc;
        _userPermissionService = userPermissionService;
        _currentUser = currentUser;
    }

    [HttpGet]       public async Task<IActionResult> GetAll()    => Ok(await _svc.GetAllAsync());
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id) =>
        (await _svc.GetByIdAsync(id)) is PaisDto dto ? Ok(dto) : NotFound();

    /// <summary>
    /// Obtiene los países asignados al usuario actual basado en sus empresas
    /// </summary>
    [HttpGet("assigned")]
    public async Task<IActionResult> GetAssignedCountries()
    {
        var countries = await _userPermissionService.GetAssignedCountriesAsync(_currentUser.UserId);
        return Ok(countries);
    }

    [HttpPost] public async Task<IActionResult> Create(CreatePaisDto dto)
    {
        var cr = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = cr.PaisId }, cr);
    }

    [HttpPut("{id}")] public async Task<IActionResult> Update(int id, UpdatePaisDto dto)
    {
        if (dto.PaisId != id) return BadRequest();
        return (await _svc.UpdateAsync(dto)) is PaisDto upd ? Ok(upd) : NotFound();
    }

    [HttpDelete("{id}")] public async Task<IActionResult> Delete(int id) =>
        (await _svc.DeleteAsync(id)) ? NoContent() : NotFound();
}
