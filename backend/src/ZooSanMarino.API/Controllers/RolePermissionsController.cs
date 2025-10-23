using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;


[ApiController]
[Route("api/Role/{roleId:int}/[controller]")]
public class PermissionsController : ControllerBase
{
    private readonly IRoleService _roles;
    public PermissionsController(IRoleService roles) => _roles = roles;

    public record KeysDto(string[] Keys);

    // Agregar permisos (idempotente)
    [HttpPost("assign")]
    public async Task<IActionResult> Assign(int roleId, [FromBody] KeysDto body)
    {
        var res = await _roles.AddPermissionsAsync(roleId, body.Keys);
        return res is null ? NotFound() : Ok(res);
    }

    // Quitar permisos (idempotente)
    [HttpPost("unassign")]
    public async Task<IActionResult> Unassign(int roleId, [FromBody] KeysDto body)
    {
        var res = await _roles.RemovePermissionsAsync(roleId, body.Keys);
        return res is null ? NotFound() : Ok(res);
    }

    // Reemplazar el set completo de permisos del rol
    [HttpPut]
    public async Task<IActionResult> Replace(int roleId, [FromBody] KeysDto body)
    {
        var res = await _roles.ReplacePermissionsAsync(roleId, body.Keys);
        return res is null ? NotFound() : Ok(res);
    }
}
