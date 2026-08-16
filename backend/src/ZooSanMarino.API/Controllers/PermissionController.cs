using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Catálogo GLOBAL de permisos: las keys que existen en todo el sistema, compartidas por todas las
/// empresas. Qué permisos habilita cada empresa es otra cosa y vive en
/// <c>CompanyPermissionController</c>.
/// </summary>
/// <remarks>
/// <b>Lecturas abiertas a cualquier sesión</b>: el módulo de Roles necesita el catálogo para poder
/// ofrecer permisos al armar un rol. <b>Escrituras solo para el administrador de la aplicación</b>
/// (policy <c>AdminAplicacion</c>): borrar o renombrar una key acá se lo lleva puesto a todos los
/// países a la vez. Antes este controller no tenía un solo <c>[Authorize]</c> — lo cubría la
/// FallbackPolicy, que solo pide token válido, así que cualquier usuario logueado podía escribirlo.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class PermissionController : ControllerBase
{
    private readonly IPermissionService _svc;
    public PermissionController(IPermissionService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _svc.GetAllAsync());

    [HttpGet("keys")]
    public async Task<IActionResult> GetKeys() => Ok(await _svc.GetAllKeysAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => (await _svc.GetByIdAsync(id)) is PermissionDto dto ? Ok(dto) : NotFound();

    [HttpPost]
    [Authorize(Policy = "AdminAplicacion")]
    public async Task<IActionResult> Create(CreatePermissionDto dto)
    {
        var created = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "AdminAplicacion")]
    public async Task<IActionResult> Update(int id, UpdatePermissionDto dto)
    {
        if (id != dto.Id) return BadRequest("Ids no coinciden.");
        var updated = await _svc.UpdateAsync(dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminAplicacion")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _svc.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }
}
