// src/ZooSanMarino.API/Controllers/UsersController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IUserService _userService;
    private readonly IUserFarmService _userFarmService;

    public UsersController(IAuthService auth, IUserService userService, IUserFarmService userFarmService)
    {
        _auth = auth;
        _userService = userService;
        _userFarmService = userFarmService;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Crear nuevo usuario (campos obligatorios)
    // ─────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] RegisterDto dto)
    {
        try
        {
            // Mantén el flujo de registro para garantizar obligatoriedad y consistencia (login + vínculos).
            var result = await _auth.RegisterAsync(dto);
            return Created(string.Empty, result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Listado (resumen)
    // ─────────────────────────────────────────────────────────────────────
    [HttpGet]
    [ProducesResponseType(typeof(List<UserListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        var users = await _userService.GetUsersAsync();
        return Ok(users);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Detalle por ID
    // ─────────────────────────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _userService.GetByIdAsync(id);
        return user is not null ? Ok(user) : NotFound();
    }

    // ─────────────────────────────────────────────────────────────────────
    // UPDATE completo (PUT) - opcional si quieres exigir objeto "completo"
    // ─────────────────────────────────────────────────────────────────────
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        try
        {
            var updated = await _userService.UpdateAsync(id, dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // UDDI genérico (PATCH): manda solo lo que quieras cambiar
    // ─────────────────────────────────────────────────────────────────────
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(Guid id, [FromBody] UpdateUserDto dto)
    {
        // Misma lógica que UpdateAsync, pero semánticamente correcto para "parcial"
        try
        {
            var updated = await _userService.UpdateAsync(id, dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Cambiar contraseña (endpoint dedicado)
    // ─────────────────────────────────────────────────────────────────────
    [HttpPatch("{id:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordDto dto)
    {
        try
        {
            await _auth.ChangePasswordAsync(id, dto);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Eliminar usuario en cascada
    // ─────────────────────────────────────────────────────────────────────
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _userService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Gestión de granjas del usuario
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Obtiene las granjas asociadas a un usuario específico
    /// </summary>
    [HttpGet("{id:guid}/farms")]
    [ProducesResponseType(typeof(UserFarmsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserFarms(Guid id)
    {
        try
        {
            var result = await _userFarmService.GetUserFarmsAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Asocia granjas a un usuario específico
    /// </summary>
    [HttpPost("{id:guid}/farms")]
    [ProducesResponseType(typeof(UserFarmDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssociateUserFarms(Guid id, [FromBody] AssociateUserFarmsDto dto)
    {
        try
        {
            // Validar que el ID del DTO coincida con el de la URL
            if (dto.UserId != id)
                return BadRequest(new { message = "El ID del usuario en el cuerpo no coincide con el de la URL." });

            // Obtener el ID del usuario autenticado para auditoría
            var createdByUserId = GetCurrentUserId();
            if (createdByUserId == null)
                return Unauthorized(new { message = "No se pudo determinar el ID del usuario autenticado." });

            var result = await _userFarmService.AssociateUserFarmsAsync(dto, createdByUserId.Value);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Reemplaza todas las granjas asociadas a un usuario
    /// </summary>
    [HttpPut("{id:guid}/farms")]
    [ProducesResponseType(typeof(UserFarmDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplaceUserFarms(Guid id, [FromBody] int[] farmIds)
    {
        try
        {
            var createdByUserId = GetCurrentUserId();
            if (createdByUserId == null)
                return Unauthorized(new { message = "No se pudo determinar el ID del usuario autenticado." });

            var result = await _userFarmService.ReplaceUserFarmsAsync(id, farmIds, createdByUserId.Value);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Elimina la asociación entre un usuario y una granja específica
    /// </summary>
    [HttpDelete("{id:guid}/farms/{farmId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveUserFarm(Guid id, int farmId)
    {
        try
        {
            var deleted = await _userFarmService.DeleteUserFarmAsync(id, farmId);
            if (!deleted)
                return NotFound(new { message = "Asociación usuario-granja no encontrada." });

            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Actualiza los permisos de un usuario en una granja específica
    /// </summary>
    [HttpPatch("{id:guid}/farms/{farmId:int}")]
    [ProducesResponseType(typeof(UserFarmDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUserFarmPermissions(Guid id, int farmId, [FromBody] UpdateUserFarmDto dto)
    {
        try
        {
            var result = await _userFarmService.UpdateUserFarmAsync(id, farmId, dto);
            if (result == null)
                return NotFound(new { message = "Asociación usuario-granja no encontrada." });

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Verifica si un usuario tiene acceso a una granja específica
    /// </summary>
    [HttpGet("{id:guid}/farms/{farmId:int}/access")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckUserFarmAccess(Guid id, int farmId)
    {
        try
        {
            var hasAccess = await _userFarmService.HasUserAccessToFarmAsync(id, farmId);
            return Ok(new { hasAccess });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene todas las granjas accesibles para un usuario (directas + por compañía)
    /// </summary>
    [HttpGet("{id:guid}/accessible-farms")]
    [ProducesResponseType(typeof(UserFarmLiteDto[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserAccessibleFarms(Guid id)
    {
        try
        {
            var farms = await _userFarmService.GetUserAccessibleFarmsAsync(id);
            return Ok(farms);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Helper para obtener el ID del usuario autenticado
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? User.FindFirstValue("uid");

        return Guid.TryParse(idStr, out var userId) ? userId : null;
    }
}
