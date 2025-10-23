// src/ZooSanMarino.API/Controllers/RolesController.cs
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
public class RolesController : ControllerBase
{
    // NOTA: Este servicio orquestador unifica lo de roles, permisos y menús.
    // Puedes implementarlo como un “façade” que internamente use tus servicios actuales
    // o como un único servicio concreto que reemplace a los existentes.
    private readonly IRoleCompositeService _svc;

    public RolesController(IRoleCompositeService svc) => _svc = svc;

    // ======== ROLES ========

    [HttpGet]
    [Authorize(Policy = "CanManageRoles")]
    [ProducesResponseType(typeof(IEnumerable<RoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? q = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await _svc.Roles_GetAllAsync(q, page, pageSize));

    [HttpGet("{id:int}")]
    [Authorize(Policy = "CanManageRoles")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
        => (await _svc.Roles_GetByIdAsync(id)) is RoleDto dto ? Ok(dto) : NotFound();

    [HttpPost]
    [Authorize(Policy = "CanManageRoles")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var created = await _svc.Roles_CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "CanManageRoles")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRoleDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (id != dto.Id) return BadRequest("El id de la ruta no coincide con el del cuerpo.");
        var upd = await _svc.Roles_UpdateAsync(dto);
        return upd is null ? NotFound() : Ok(upd);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "CanManageRoles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
        => (await _svc.Roles_DeleteAsync(id)) ? NoContent() : NotFound();

    // ======== PERMISOS (Catálogo & Asignación al rol) ========

    public record KeysDto(string[] Keys);

    [HttpGet("permissions")]
    [Authorize(Policy = "CanManageRoles")]
    [ProducesResponseType(typeof(IEnumerable<PermissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PermissionsCatalog()
        => Ok(await _svc.Permissions_GetAllAsync());

    [HttpGet("{roleId:int}/permissions")]
    [Authorize(Policy = "CanManageRoles")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRolePermissions(int roleId)
        => (await _svc.Roles_GetPermissionsAsync(roleId)) is { } keys ? Ok(keys) : NotFound();

    [HttpPost("{roleId:int}/permissions/assign")]
    [Authorize(Policy = "CanManageRoles")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignPermissions(int roleId, [FromBody] KeysDto body)
    {
        if (body?.Keys is null || body.Keys.Length == 0)
            return BadRequest("Debe especificar al menos un permiso.");
        var res = await _svc.Roles_AddPermissionsAsync(roleId, body.Keys);
        return res is null ? NotFound() : Ok(res);
    }

    [HttpPost("{roleId:int}/permissions/unassign")]
    [Authorize(Policy = "CanManageRoles")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignPermissions(int roleId, [FromBody] KeysDto body)
    {
        if (body?.Keys is null || body.Keys.Length == 0)
            return BadRequest("Debe especificar al menos un permiso.");
        var res = await _svc.Roles_RemovePermissionsAsync(roleId, body.Keys);
        return res is null ? NotFound() : Ok(res);
    }

    [HttpPut("{roleId:int}/permissions")]
    [Authorize(Policy = "CanManageRoles")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplacePermissions(int roleId, [FromBody] KeysDto body)
    {
        if (body?.Keys is null) return BadRequest("Debe enviar el arreglo Keys (puede estar vacío).");
        var res = await _svc.Roles_ReplacePermissionsAsync(roleId, body.Keys);
        return res is null ? NotFound() : Ok(res);
    }

    // ======== MENÚS (Catálogo, CRUD y filtrado por usuario) ========

    [HttpGet("menus/tree")]
    [Authorize(Policy = "CanManageMenus")]
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MenusTree()
        => Ok(await _svc.Menus_GetTreeAsync());

    [HttpGet("menus/me")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MenusForCurrentUser([FromQuery] int? companyId = null)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? User.FindFirstValue("uid");

        if (!Guid.TryParse(idStr, out var userId))
            return Unauthorized(new { message = "No se pudo determinar el GUID del usuario." });

        return Ok(await _svc.Menus_GetForUserAsync(userId, companyId));
    }

    [HttpGet("menus/user/{userId:guid}")]
    [Authorize(Policy = "CanManageUsers")]
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MenusForUser(Guid userId, [FromQuery] int? companyId = null)
        => Ok(await _svc.Menus_GetForUserAsync(userId, companyId));

    [HttpPost("menus")]
    [Authorize(Policy = "CanManageMenus")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMenu([FromBody] CreateMenuDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var created = await _svc.Menus_CreateAsync(dto);
        return Created($"/api/roles/menus/tree", created);
    }

    [HttpPut("menus/{id:int}")]
    [Authorize(Policy = "CanManageMenus")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMenu(int id, [FromBody] UpdateMenuDto dto)
    {
        if (id != dto.Id) return BadRequest("El id de la ruta no coincide con el del cuerpo.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var upd = await _svc.Menus_UpdateAsync(dto);
        return upd is null ? NotFound() : Ok(upd);
    }

    [HttpDelete("menus/{id:int}")]
    [Authorize(Policy = "CanManageMenus")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteMenu(int id)
        => (await _svc.Menus_DeleteAsync(id)) ? NoContent() : NotFound();
}
