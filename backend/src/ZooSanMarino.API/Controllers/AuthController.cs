// src/ZooSanMarino.API/Controllers/AuthController.cs
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
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService auth, ILogger<AuthController> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    /// <summary>Inicia sesión con correo y contraseña.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var result = await _auth.LoginAsync(dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Login fallido para {Email}", dto.Email);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en /api/Auth/login");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error interno" });
        }
    }

    /// <summary>Registro por email/password.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var result = await _auth.RegisterAsync(dto);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Registro fallido para {Email}", dto.Email);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en /api/Auth/register");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error interno" });
        }
    }

    /// <summary>Cambia la contraseña del usuario autenticado.</summary>
    [Authorize]
    [HttpPost("change-password")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var userId = GetUserIdOrUnauthorized(out var unauthorized);
        if (unauthorized is not null) return unauthorized;

        try
        {
            await _auth.ChangePasswordAsync(userId!.Value, dto);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Cambia el email del login (requiere contraseña actual).</summary>
    [Authorize]
    [HttpPost("change-email")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var userId = GetUserIdOrUnauthorized(out var unauthorized);
        if (unauthorized is not null) return unauthorized;

        try
        {
            await _auth.ChangeEmailAsync(userId!.Value, dto);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ya está en uso", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Bootstrap de sesión: perfil, compañías, roles, permisos y menú.</summary>
    [Authorize]
    [HttpGet("session")]
    [ProducesResponseType(typeof(SessionBootstrapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Session([FromQuery] int? companyId = null, CancellationToken ct = default)
    {
        var userId = GetUserIdOrUnauthorized(out var unauthorized);
        if (unauthorized is not null) return unauthorized;

        var dto = await _auth.GetSessionAsync(userId!.Value, companyId);
        return Ok(dto);
    }

    /// <summary>Perfil básico del usuario autenticado (desde claims).</summary>
    [Authorize]
    [HttpGet("profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Profile()
    {
        var userId   = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var email    = User.FindFirst("email")?.Value
                       ?? User.FindFirstValue(ClaimTypes.Email)
                       ?? User.Identity?.Name
                       ?? string.Empty;
        var first    = User.FindFirst("firstName")?.Value ?? string.Empty;
        var sur      = User.FindFirst("surName")?.Value   ?? string.Empty;
        var roles    = User.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray();
        var companies= User.FindAll("company").Select(c => c.Value).Distinct().ToArray();
        var companyIds = User.FindAll("company_id").Select(c => c.Value).Distinct().ToArray();
        var permissions = User.FindAll("permission").Select(c => c.Value).Distinct().ToArray();

        return Ok(new
        {
            userId,
            email,
            firstName = first,
            surName   = sur,
            roles,
            companies,
            companyIds,
            permissions
        });
    }

    // Endpoint temporalmente comentado para debug
    /*
    /// <summary>Recuperación de contraseña por email.</summary>
    [AllowAnonymous]
    [HttpPost("recover-password")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(PasswordRecoveryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RecoverPassword([FromBody] PasswordRecoveryRequestDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var result = await _auth.RecoverPasswordAsync(dto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en /api/Auth/recover-password");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error interno" });
        }
    }
    */

    /// <summary>Ping simple para debug (sin autorización).</summary>
    [AllowAnonymous]
    [HttpGet("ping-simple")]
    public IActionResult PingSimple() => Ok(new { ok = true, at = DateTime.UtcNow });

    /// <summary>Ping autenticado (para probar token desde el front).</summary>
    [Authorize]
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { ok = true, at = DateTime.UtcNow });

    // === Helpers ===
    private Guid? GetUserIdOrUnauthorized(out IActionResult? unauthorized)
    {
        unauthorized = null;
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? User.FindFirstValue("uid");

        if (!Guid.TryParse(idStr, out var guid))
        {
            unauthorized = Unauthorized(new { message = "Usuario no autenticado" });
            return null;
        }
        return guid;
    }
}
