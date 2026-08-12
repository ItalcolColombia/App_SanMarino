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
    private readonly ICompanyMenuService _companyMenuSvc;
    private readonly ICompanyPermissionService _companyPermissionSvc;
    private readonly ICurrentUser _currentUser;

    public CompanyController(
        ICompanyService svc,
        ICompanyMenuService companyMenuSvc,
        ICompanyPermissionService companyPermissionSvc,
        ICurrentUser currentUser)
    {
        _svc = svc;
        _companyMenuSvc = companyMenuSvc;
        _companyPermissionSvc = companyPermissionSvc;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _svc.GetAllAsync());

    /// <summary>
    /// Obtiene TODAS las empresas sin filtro para administración
    /// </summary>
    /// <remarks>Ruta "global" (no "admin"): AWS WAF AdminProtection bloquea cualquier path con /admin.</remarks>
    [HttpGet("global")]
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

    /// <summary>Obtiene los menús asignados a la empresa (árbol con estado habilitado).</summary>
    [HttpGet("{id:int}/menus")]
    public async Task<IActionResult> GetCompanyMenus(int id)
    {
        var menus = await _companyMenuSvc.GetMenusForCompanyAsync(id);
        return Ok(menus);
    }

    /// <summary>Asigna o actualiza los menús de la empresa.</summary>
    [HttpPut("{id:int}/menus")]
    public async Task<IActionResult> SetCompanyMenus(int id, [FromBody] SetCompanyMenusRequest request)
    {
        if (request == null) return BadRequest();
        await _companyMenuSvc.SetCompanyMenusAsync(id, request);
        return NoContent();
    }

    /// <summary>
    /// Catálogo COMPLETO de permisos con el estado (habilitado / no) para esta empresa y cuántos
    /// roles suyos ya usan cada uno.
    /// </summary>
    [HttpGet("{id:int}/permissions")]
    public async Task<IActionResult> GetCompanyPermissions(int id)
    {
        var permisos = await _companyPermissionSvc.GetPermissionsForCompanyAsync(id);
        return Ok(permisos);
    }

    /// <summary>
    /// Fija los permisos habilitados de la empresa. Lo desmarcado se apaga, NO se borra de los roles
    /// que ya lo tenían: esas asignaciones quedan huérfanas (no se ofrecen ni viajan en el login) y
    /// la UI de roles las muestra para que el admin las limpie a conciencia.
    /// </summary>
    [HttpPut("{id:int}/permissions")]
    public async Task<IActionResult> SetCompanyPermissions(int id, [FromBody] SetCompanyPermissionsRequest request)
    {
        if (request == null) return BadRequest();
        await _companyPermissionSvc.SetPermissionsForCompanyAsync(id, request);
        return NoContent();
    }

    /// <summary>Actualiza orden y jerarquía (parent) de los menús de la empresa.</summary>
    [HttpPut("{id:int}/menus/structure")]
    public async Task<IActionResult> UpdateCompanyMenuStructure(int id, [FromBody] UpdateCompanyMenuStructureRequest request)
    {
        if (request == null) return BadRequest();
        await _companyMenuSvc.UpdateCompanyMenuStructureAsync(id, request);
        return NoContent();
    }
}
