// src/ZooSanMarino.API/Controllers/MenuController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MenuController : ControllerBase
{
    private readonly IMenuService _svc;

    public MenuController(IMenuService svc) => _svc = svc;

    /// <summary>Árbol completo de menús (sin filtrar). Útil para administración.</summary>
    [HttpGet("tree")]
    [Authorize(Policy = "CanManageMenus")] // quítalo si no usas policies
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree() => Ok(await _svc.GetTreeAsync());

    /// <summary>Menú filtrado por permisos del usuario autenticado. Acepta companyId opcional.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetForCurrentUser([FromQuery] int? companyId = null)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? User.FindFirstValue("uid");

        if (!Guid.TryParse(idStr, out var userId))
            return Unauthorized(new { message = "No se pudo determinar el GUID del usuario." });

        var data = await _svc.GetForUserAsync(userId, companyId);
        return Ok(data);
    }

    /// <summary>Menú filtrado por permisos para un usuario específico (administración).</summary>
    [HttpGet("user/{userId:guid}")]
    [Authorize(Policy = "CanManageUsers")]
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForUser(Guid userId, [FromQuery] int? companyId = null)
        => Ok(await _svc.GetForUserAsync(userId, companyId));

    /// <summary>Crea un ítem de menú.</summary>
    [HttpPost]
    [Authorize(Policy = "CanManageMenus")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateMenuDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var created = await _svc.CreateAsync(dto);
        // No hay endpoint "get by id"; devolvemos Created con Location genérica del recurso
        return Created($"/api/menu/tree", created);
    }

    /// <summary>Actualiza un ítem de menú.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "CanManageMenus")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMenuDto dto)
    {
        if (id != dto.Id) return BadRequest("El id de la ruta no coincide con el del cuerpo.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var upd = await _svc.UpdateAsync(dto);
        return upd is null ? NotFound() : Ok(upd);
    }

    /// <summary>Elimina un ítem de menú. No permite eliminar si tiene hijos.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "CanManageMenus")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
        => (await _svc.DeleteAsync(id)) ? NoContent() : NotFound();
}
