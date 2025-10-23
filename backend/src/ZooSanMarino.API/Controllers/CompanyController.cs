// src/ZooSanMarino.API/Controllers/CompanyController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.API.Infrastructure;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompanyController : ControllerBase
{
    private readonly ICompanyService _svc;
    private readonly ICurrentUser _currentUser;
    
    public CompanyController(ICompanyService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _svc.GetAllAsync());

    /// <summary>
    /// Obtiene TODAS las empresas sin filtro para administración
    /// </summary>
    [HttpGet("admin")]
    public async Task<IActionResult> GetAllForAdmin() =>
        Ok(await _svc.GetAllForAdminAsync());

    /// <summary>
    /// Endpoint temporal para debug - muestra información del usuario actual
    /// </summary>
    [HttpGet("debug")]
    public IActionResult GetDebugInfo()
    {
        var debugInfo = new
        {
            UserId = _currentUser.UserId,
            CompanyId = _currentUser.CompanyId,
            ActiveCompanyName = _currentUser.ActiveCompanyName,
            Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            Timestamp = DateTime.UtcNow
        };
        
        Console.WriteLine($"CompanyController.Debug - UserId: {_currentUser.UserId}");
        Console.WriteLine($"CompanyController.Debug - CompanyId: {_currentUser.CompanyId}");
        Console.WriteLine($"CompanyController.Debug - ActiveCompanyName: '{_currentUser.ActiveCompanyName}'");
        
        return Ok(debugInfo);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        (await _svc.GetByIdAsync(id)) is CompanyDto dto
          ? Ok(dto)
          : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create(CreateCompanyDto dto)
    {
        var cr = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = cr.Id }, cr);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateCompanyDto dto)
    {
        if (dto.Id != id) return BadRequest();
        return (await _svc.UpdateAsync(dto)) is CompanyDto upd
          ? Ok(upd)
          : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        (await _svc.DeleteAsync(id)) ? NoContent() : NotFound();
}
