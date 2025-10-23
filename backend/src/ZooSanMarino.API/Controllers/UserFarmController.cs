using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
// TEMPORAL: Removido [Authorize] para permitir registro sin autenticación
// TODO: Restaurar [Authorize] cuando se resuelva el problema de autenticación
// [Authorize]
public class UserFarmController : ControllerBase
{
    private readonly IUserFarmService _userFarmService;

    public UserFarmController(IUserFarmService userFarmService)
    {
        _userFarmService = userFarmService;
    }

    /// <summary>
    /// Obtiene todas las granjas asociadas a un usuario específico
    /// </summary>
    [HttpGet("user/{userId:guid}/farms")]
    [ProducesResponseType(typeof(UserFarmsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserFarms(Guid userId)
    {
        try
        {
            var result = await _userFarmService.GetUserFarmsAsync(userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene todas las granjas asociadas al usuario autenticado
    /// </summary>
    [HttpGet("me/farms")]
    [ProducesResponseType(typeof(UserFarmsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyFarms()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "No se pudo determinar el ID del usuario." });

        try
        {
            var result = await _userFarmService.GetUserFarmsAsync(userId.Value);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene todos los usuarios asociados a una granja específica
    /// </summary>
    [HttpGet("farm/{farmId:int}/users")]
    [ProducesResponseType(typeof(FarmUsersResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFarmUsers(int farmId)
    {
        try
        {
            var result = await _userFarmService.GetFarmUsersAsync(farmId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Crea una nueva asociación usuario-granja
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserFarmDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUserFarm([FromBody] CreateUserFarmDto dto)
    {
        try
        {
            // Debug logging
            Console.WriteLine("=== CreateUserFarm Debug ===");
            Console.WriteLine($"Request Body: UserId={dto.UserId}, FarmId={dto.FarmId}, IsAdmin={dto.IsAdmin}, IsDefault={dto.IsDefault}");
            Console.WriteLine($"Authorization Header: {Request.Headers.Authorization}");
            Console.WriteLine($"User.Identity.IsAuthenticated: {User.Identity?.IsAuthenticated}");
            
            // TEMPORAL: Deshabilitar validación de token para permitir registro
            // TODO: Restaurar validación de token cuando se resuelva el problema de autenticación
            var createdByUserId = GetCurrentUserId();
            
            // Si no se puede obtener el ID del usuario del token, usar el mismo usuario que se está asignando
            if (createdByUserId == null)
            {
                Console.WriteLine("=== WARNING: No se pudo obtener ID del usuario del token, usando el mismo usuario ===");
                createdByUserId = dto.UserId; // Usar el mismo usuario que se está asignando
            }
            
            Console.WriteLine($"CreatedByUserId final: {createdByUserId}");
            Console.WriteLine("=== End CreateUserFarm Debug ===");

            var result = await _userFarmService.CreateUserFarmAsync(dto, createdByUserId.Value);
            return CreatedAtAction(nameof(GetUserFarm), new { userId = dto.UserId, farmId = dto.FarmId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Actualiza una asociación usuario-granja existente
    /// </summary>
    [HttpPut("user/{userId:guid}/farm/{farmId:int}")]
    [ProducesResponseType(typeof(UserFarmDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUserFarm(Guid userId, int farmId, [FromBody] UpdateUserFarmDto dto)
    {
        try
        {
            var result = await _userFarmService.UpdateUserFarmAsync(userId, farmId, dto);
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
    /// Elimina una asociación usuario-granja
    /// </summary>
    [HttpDelete("user/{userId:guid}/farm/{farmId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUserFarm(Guid userId, int farmId)
    {
        try
        {
            var deleted = await _userFarmService.DeleteUserFarmAsync(userId, farmId);
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
    /// Asocia múltiples granjas a un usuario
    /// </summary>
    [HttpPost("user/{userId:guid}/associate-farms")]
    [ProducesResponseType(typeof(UserFarmDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssociateUserFarms(Guid userId, [FromBody] AssociateUserFarmsDto dto)
    {
        try
        {
            // Validar que el userId del DTO coincida con el de la URL
            if (dto.UserId != userId)
                return BadRequest(new { message = "El ID del usuario en el cuerpo no coincide con el de la URL." });

            var createdByUserId = GetCurrentUserId();
            if (createdByUserId == null)
                return Unauthorized(new { message = "No se pudo determinar el ID del usuario." });

            var result = await _userFarmService.AssociateUserFarmsAsync(dto, createdByUserId.Value);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Asocia múltiples usuarios a una granja
    /// </summary>
    [HttpPost("farm/{farmId:int}/associate-users")]
    [ProducesResponseType(typeof(UserFarmDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssociateFarmUsers(int farmId, [FromBody] AssociateFarmUsersDto dto)
    {
        try
        {
            // Validar que el farmId del DTO coincida con el de la URL
            if (dto.FarmId != farmId)
                return BadRequest(new { message = "El ID de la granja en el cuerpo no coincide con el de la URL." });

            var createdByUserId = GetCurrentUserId();
            if (createdByUserId == null)
                return Unauthorized(new { message = "No se pudo determinar el ID del usuario." });

            var result = await _userFarmService.AssociateFarmUsersAsync(dto, createdByUserId.Value);
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
    [HttpPut("user/{userId:guid}/replace-farms")]
    [ProducesResponseType(typeof(UserFarmDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplaceUserFarms(Guid userId, [FromBody] int[] farmIds)
    {
        try
        {
            var createdByUserId = GetCurrentUserId();
            if (createdByUserId == null)
                return Unauthorized(new { message = "No se pudo determinar el ID del usuario." });

            var result = await _userFarmService.ReplaceUserFarmsAsync(userId, farmIds, createdByUserId.Value);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Reemplaza todos los usuarios asociados a una granja
    /// </summary>
    [HttpPut("farm/{farmId:int}/replace-users")]
    [ProducesResponseType(typeof(UserFarmDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplaceFarmUsers(int farmId, [FromBody] Guid[] userIds)
    {
        try
        {
            var createdByUserId = GetCurrentUserId();
            if (createdByUserId == null)
                return Unauthorized(new { message = "No se pudo determinar el ID del usuario." });

            var result = await _userFarmService.ReplaceFarmUsersAsync(farmId, userIds, createdByUserId.Value);
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
    [HttpGet("user/{userId:guid}/farm/{farmId:int}/access")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckUserAccess(Guid userId, int farmId)
    {
        try
        {
            var hasAccess = await _userFarmService.HasUserAccessToFarmAsync(userId, farmId);
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
    [HttpGet("user/{userId:guid}/accessible-farms")]
    [ProducesResponseType(typeof(UserFarmLiteDto[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserAccessibleFarms(Guid userId)
    {
        try
        {
            var farms = await _userFarmService.GetUserAccessibleFarmsAsync(userId);
            return Ok(farms);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene todas las granjas accesibles para el usuario autenticado
    /// </summary>
    [HttpGet("me/accessible-farms")]
    [ProducesResponseType(typeof(UserFarmLiteDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyAccessibleFarms()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "No se pudo determinar el ID del usuario." });

        try
        {
            var farms = await _userFarmService.GetUserAccessibleFarmsAsync(userId.Value);
            return Ok(farms);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene una asociación específica usuario-granja
    /// </summary>
    [HttpGet("user/{userId:guid}/farm/{farmId:int}")]
    [ProducesResponseType(typeof(UserFarmDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserFarm(Guid userId, int farmId)
    {
        try
        {
            var userFarms = await _userFarmService.GetUserFarmsAsync(userId);
            var userFarm = userFarms.Farms.FirstOrDefault(uf => uf.FarmId == farmId);
            
            if (userFarm == null)
                return NotFound(new { message = "Asociación usuario-granja no encontrada." });

            return Ok(userFarm);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Helper para obtener el ID del usuario autenticado
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        // Debug: Log all claims
        Console.WriteLine("=== DEBUG: GetCurrentUserId ===");
        Console.WriteLine($"User.Identity.IsAuthenticated: {User.Identity?.IsAuthenticated}");
        Console.WriteLine($"User.Identity.Name: {User.Identity?.Name}");
        
        // Log all claims
        foreach (var claim in User.Claims)
        {
            Console.WriteLine($"Claim: {claim.Type} = {claim.Value}");
        }
        
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? User.FindFirstValue("uid");

        Console.WriteLine($"NameIdentifier: {User.FindFirstValue(ClaimTypes.NameIdentifier)}");
        Console.WriteLine($"Sub: {User.FindFirstValue("sub")}");
        Console.WriteLine($"Uid: {User.FindFirstValue("uid")}");
        Console.WriteLine($"Final idStr: {idStr}");

        var result = Guid.TryParse(idStr, out var userId) ? (Guid?)userId : null;
        Console.WriteLine($"Final userId: {result}");
        Console.WriteLine("=== END DEBUG ===");

        return result;
    }
}
