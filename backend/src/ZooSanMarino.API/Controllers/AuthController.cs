// src/ZooSanMarino.API/Controllers/AuthController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Services;
using ZooSanMarino.API.Services;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ILogger<AuthController> _logger;
    private readonly EncryptionService _encryption;
    private readonly IRecaptchaService _recaptcha;
    private readonly IConfiguration _configuration;
    private readonly InputSanitizerService _sanitizer;
    private readonly IEmailQueueService _emailQueue;

    public AuthController(
        IAuthService auth, 
        ILogger<AuthController> logger, 
        EncryptionService encryption,
        IRecaptchaService recaptcha,
        IConfiguration configuration,
        InputSanitizerService sanitizer,
        IEmailQueueService emailQueue)
    {
        _auth = auth;
        _logger = logger;
        _encryption = encryption;
        _recaptcha = recaptcha;
        _configuration = configuration;
        _sanitizer = sanitizer;
        _emailQueue = emailQueue;
    }

    /// <summary>Inicia sesión con correo y contraseña (datos encriptados).</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Login([FromBody] EncryptedRequestDto encryptedRequest, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(encryptedRequest?.EncryptedData))
            return BadRequest(new { message = "Datos encriptados no proporcionados" });

        try
        {
            // 1. Desencriptar datos recibidos del frontend
            var dto = _encryption.DecryptFromFrontend<LoginDto>(encryptedRequest.EncryptedData);

            // 1.1. Sanitizar datos después de desencriptar (segunda capa de seguridad)
            // NOTA: No sanitizar contraseñas porque pueden contener cualquier carácter especial
            // NOTA: No sanitizar tokens de reCAPTCHA porque son cadenas firmadas por Google que pueden contener caracteres especiales
            dto.Email = _sanitizer.Sanitize(dto.Email);
            // dto.Password NO se sanitiza - las contraseñas pueden tener cualquier carácter
            // dto.RecaptchaToken NO se sanitiza - los tokens de Google pueden contener cualquier carácter especial

            // 2. Validar datos desencriptados
            // Log para debugging (en producción, remover o usar nivel de log apropiado)
            _logger.LogInformation("Datos desencriptados: Email={Email}, Password presente={HasPassword}", 
                dto?.Email ?? "null", 
                !string.IsNullOrWhiteSpace(dto?.Password));

            if (dto == null)
                return BadRequest(new { message = "Error al desencriptar: objeto nulo" });

            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                _logger.LogWarning("Datos desencriptados inválidos: Email={Email}, Password length={PasswordLength}", 
                    dto.Email ?? "null", 
                    dto.Password?.Length ?? 0);
                return BadRequest(new { message = "Email y contraseña son requeridos" });
            }

            // 2.1. Validar ModelState después de sanitizar (validación de atributos)
            if (!TryValidateModel(dto))
            {
                return ValidationProblem(ModelState);
            }

            // 3. Validar reCAPTCHA (solo en producción)
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "";
            var isProduction = !environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
            var recaptchaEnabledValue = _configuration["Recaptcha:Enabled"];
            var recaptchaEnabled = !string.IsNullOrWhiteSpace(recaptchaEnabledValue) && 
                                   bool.TryParse(recaptchaEnabledValue, out var enabled) && enabled;
            
            if (isProduction && recaptchaEnabled)
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString()
                            ?? HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                            ?? HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(dto.RecaptchaToken))
                {
                    _logger.LogWarning("reCAPTCHA token faltante en producción para {Email}", dto.Email);
                    return BadRequest(new { message = "Validación de seguridad requerida. Por favor, completa el reCAPTCHA." });
                }

                var isValidRecaptcha = await _recaptcha.ValidateRecaptchaAsync(dto.RecaptchaToken, clientIp);
                if (!isValidRecaptcha)
                {
                    _logger.LogWarning("reCAPTCHA inválido para {Email} desde {ClientIp}", dto.Email, clientIp);
                    return BadRequest(new { message = "Validación de seguridad fallida. Por favor, intenta nuevamente." });
                }
            }

            // 4. Procesar login
            var result = await _auth.LoginAsync(dto);

            // 5. Encriptar respuesta antes de enviarla al frontend
            var encryptedResponse = _encryption.EncryptForFrontend(result);

            // 6. Retornar respuesta encriptada como texto plano
            return Content(encryptedResponse, "text/plain");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Login fallido");
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
            // Sanitizar todos los campos del DTO antes de procesar
            // NOTA: No sanitizar contraseñas porque pueden contener cualquier carácter especial
            dto.Email = _sanitizer.Sanitize(dto.Email);
            // dto.Password NO se sanitiza - las contraseñas pueden tener cualquier carácter
            dto.FirstName = _sanitizer.Sanitize(dto.FirstName);
            dto.SurName = _sanitizer.Sanitize(dto.SurName);
            dto.Cedula = _sanitizer.Sanitize(dto.Cedula);
            dto.Telefono = _sanitizer.Sanitize(dto.Telefono);
            dto.Ubicacion = _sanitizer.Sanitize(dto.Ubicacion);

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

    /// <summary>Recuperación de contraseña por email.</summary>
    [AllowAnonymous]
    [HttpPost("recover-password")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(PasswordRecoveryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RecoverPassword([FromBody] PasswordRecoveryRequestDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Validación fallida en /api/Auth/recover-password. Errores: {Errors}", 
                string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return ValidationProblem(ModelState);
        }

        try
        {
            // Sanitizar el email antes de procesar
            dto.Email = _sanitizer.Sanitize(dto.Email);
            
            _logger.LogInformation("Solicitud de recuperación de contraseña para email: {Email}", 
                dto.Email?.Substring(0, Math.Min(5, dto.Email?.Length ?? 0)) + "***");

            var result = await _auth.RecoverPasswordAsync(dto);
            
            if (result.Success)
            {
                _logger.LogInformation("Recuperación de contraseña exitosa. Email: {Email}, EmailSent: {EmailSent}, QueueId: {QueueId}",
                    dto.Email?.Substring(0, Math.Min(5, dto.Email?.Length ?? 0)) + "***", 
                    result.EmailSent, 
                    result.EmailQueueId);
            }
            else
            {
                _logger.LogWarning("Recuperación de contraseña fallida. Email: {Email}, UserFound: {UserFound}, Message: {Message}",
                    dto.Email?.Substring(0, Math.Min(5, dto.Email?.Length ?? 0)) + "***",
                    result.UserFound,
                    result.Message);
            }
            
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Argumento inválido en /api/Auth/recover-password. Email: {Email}", 
                dto.Email?.Substring(0, Math.Min(5, dto.Email?.Length ?? 0)) + "***");
            return BadRequest(new PasswordRecoveryResponseDto
            {
                Success = false,
                Message = "Los datos proporcionados no son válidos. Verifica el formato del correo electrónico.",
                UserFound = false,
                EmailSent = false
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Operación inválida en /api/Auth/recover-password. Email: {Email}", 
                dto.Email?.Substring(0, Math.Min(5, dto.Email?.Length ?? 0)) + "***");
            return BadRequest(new PasswordRecoveryResponseDto
            {
                Success = false,
                Message = ex.Message,
                UserFound = false,
                EmailSent = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error inesperado en /api/Auth/recover-password. Email: {Email}, ExceptionType: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                dto.Email?.Substring(0, Math.Min(5, dto.Email?.Length ?? 0)) + "***",
                ex.GetType().Name,
                ex.Message,
                ex.StackTrace);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new PasswordRecoveryResponseDto
            {
                Success = false,
                Message = "Ocurrió un error interno al procesar la solicitud. Por favor, intenta nuevamente más tarde.",
                UserFound = false,
                EmailSent = false
            });
        }
    }

    /// <summary>Obtiene el menú del usuario autenticado (datos encriptados).</summary>
    [Authorize]
    [HttpGet("menu")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMenu([FromQuery] int? companyId = null, CancellationToken ct = default)
    {
        var userId = GetUserIdOrUnauthorized(out var unauthorized);
        if (unauthorized is not null) return unauthorized;

        try
        {
            // 1. Obtener menú del usuario usando el servicio de autenticación
            var effectiveMenu = await _auth.GetMenuForUserAsync(userId!.Value, companyId);
            
            // 2. También obtener MenusByRole para compatibilidad
            var menusByRole = await _auth.GetMenusByRoleForUserAsync(userId.Value);
            
            // 3. Crear respuesta con menú y menusByRole
            var menuResponse = new
            {
                Menu = effectiveMenu,
                MenusByRole = menusByRole
            };
            
            // 4. Encriptar respuesta antes de enviarla al frontend
            var encryptedResponse = _encryption.EncryptForFrontend(menuResponse);
            
            // 5. Retornar respuesta encriptada como texto plano
            return Content(encryptedResponse, "text/plain");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener menú para usuario {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error al obtener el menú" });
        }
    }

    /// <summary>Obtiene el estado de envío de un correo en la cola.</summary>
    [Authorize]
    [HttpGet("email-status/{emailQueueId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetEmailStatus(int emailQueueId, CancellationToken ct = default)
    {
        try
        {
            var status = await _emailQueue.GetEmailStatusDetailAsync(emailQueueId);
            
            if (status == null)
            {
                return NotFound(new { message = "Correo no encontrado en la cola" });
            }

            // Encriptar respuesta antes de enviarla al frontend
            var encryptedResponse = _encryption.EncryptForFrontend(status);
            return Content(encryptedResponse, "text/plain");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener estado del correo: EmailQueueId={EmailQueueId}", emailQueueId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error al obtener el estado del correo" });
        }
    }

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
