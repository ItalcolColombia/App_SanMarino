// src/ZooSanMarino.API/Controllers/RolesController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.API.Infrastructure;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
// Gate real de roles/permisos. Las policies `CanManageRoles`/`CanManageMenus` de los atributos de
// abajo son, hasta hoy, `RequireAuthenticatedUser()`: NO filtran nada. El filtro de clase cubre
// todo endpoint que no se exceptúe explícitamente — ver RolesGestionFilter.cs.
[RolesGestionFilter]
public class RolesController : ControllerBase
{
    // NOTA: Este servicio orquestador unifica lo de roles, permisos y menús.
    // Puedes implementarlo como un "façade" que internamente use tus servicios actuales
    // o como un único servicio concreto que reemplace a los existentes.
    private readonly IRoleCompositeService _svc;
    private readonly ICurrentUser _currentUser;
    private readonly ZooSanMarinoContext _ctx;

    public RolesController(IRoleCompositeService svc, ICurrentUser currentUser, ZooSanMarinoContext ctx)
    {
        _svc = svc;
        _currentUser = currentUser;
        _ctx = ctx;
    }

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
    // Catálogo GLOBAL: el árbol de módulos de todas las empresas y todos los países. Lo leen la
    // pantalla de Roles y la de Empresas; el sidebar NO pasa por acá (usa menus/me).
    [CatalogoMenusLectura]
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MenusTree()
        => Ok(await _svc.Menus_GetTreeAsync());

    [HttpGet("menus/me")]
    [Authorize]
    // El menú del PROPIO usuario: alimenta el sidebar de toda la aplicación. Queda abierto.
    [RolesPermisoNoRequerido]
    [ProducesResponseType(typeof(MenuWithCountryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MenusForCurrentUser([FromQuery] int? companyId = null)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? User.FindFirstValue("uid");

        if (!Guid.TryParse(idStr, out var userId))
            return Unauthorized(new { message = "No se pudo determinar el GUID del usuario." });

        var menu = await _svc.Menus_GetForUserAsync(userId, EmpresaEfectiva(companyId));

        // Obtener información del país activo
        var paisId = _currentUser.PaisId;
        string? paisNombre = null;
        if (paisId.HasValue)
        {
            var pais = await _ctx.Paises
                .AsNoTracking()
                .Where(p => p.PaisId == paisId.Value)
                .Select(p => p.PaisNombre)
                .FirstOrDefaultAsync();
            paisNombre = pais;
        }

        // Obtener información de la empresa activa
        var activeCompanyId = _currentUser.CompanyId;
        string? companyName = _currentUser.ActiveCompanyName;

        var response = new MenuWithCountryDto
        {
            Menu = menu,
            PaisId = paisId,
            PaisNombre = paisNombre,
            CompanyId = activeCompanyId > 0 ? activeCompanyId : null,
            CompanyName = companyName
        };

        return Ok(response);
    }

    [HttpGet("menus/user/{userId:guid}")]
    [Authorize(Policy = "CanManageUsers")]
    // ⚠️ `CanManageUsers` sigue siendo "token válido y nada más". Queda fuera del alcance de este
    // trabajo a propósito: la comparte MenuController.GetForUser y endurecerla es otro cambio.
    [RolesPermisoNoRequerido]
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MenusForUser(Guid userId, [FromQuery] int? companyId = null)
        => Ok(await _svc.Menus_GetForUserAsync(userId, EmpresaEfectiva(companyId)));

    /// <summary>
    /// La empresa contra la que se resuelve el menú.
    ///
    /// <para>
    /// El sidebar llama a <c>menus/me</c> <b>sin</b> <c>companyId</c> (<c>MenuService.ensureLoaded()</c>
    /// no lo manda), y sin empresa el menú se armaba con los roles del usuario en TODAS sus empresas
    /// y sin poder aplicar el gate de <c>company_menus</c>. Se cae a <c>ICurrentUser.CompanyId</c>,
    /// que es la empresa activa que <c>ActiveCompanyMiddleware</c> ya validó contra
    /// <c>UserCompanies</c> — nunca el header crudo.
    /// </para>
    ///
    /// <para>Sin empresa resoluble queda <c>null</c> y se comporta como antes.</para>
    /// </summary>
    private int? EmpresaEfectiva(int? companyIdDeLaQuery)
    {
        if (companyIdDeLaQuery is int explicito && explicito > 0) return explicito;
        return _currentUser.CompanyId > 0 ? _currentUser.CompanyId : null;
    }

    // Las tres escrituras de abajo tocan el árbol de menús GLOBAL (el mismo que MenuController):
    // reservado al administrador de la aplicación. `CanManageMenus` solo exige sesión válida.
    [HttpPost("menus")]
    [Authorize(Policy = "AdminAplicacion")]
    [RolesPermisoNoRequerido] // AdminAplicacion ya es más estricto que este filtro.
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
    [Authorize(Policy = "AdminAplicacion")]
    [RolesPermisoNoRequerido] // AdminAplicacion ya es más estricto que este filtro.
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
    [Authorize(Policy = "AdminAplicacion")]
    [RolesPermisoNoRequerido] // AdminAplicacion ya es más estricto que este filtro.
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteMenu(int id)
        => (await _svc.Menus_DeleteAsync(id)) ? NoContent() : NotFound();
}
