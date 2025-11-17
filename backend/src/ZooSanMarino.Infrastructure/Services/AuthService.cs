// src/ZooSanMarino.Infrastructure/Services/AuthService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Application.Options;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly IPasswordHasher<Login> _hasher;
    private readonly JwtOptions _jwt;
    private readonly IRoleCompositeService _acl; // ← reemplaza a IMenuService
    private readonly IEmailService _emailService;

    public AuthService(
        ZooSanMarinoContext ctx,
        IPasswordHasher<Login> hasher,
        JwtOptions jwt,
        IRoleCompositeService acl,
        IEmailService emailService)
    {
        _ctx = ctx;
        _hasher = hasher;
        _jwt = jwt;
        _acl = acl;
        _emailService = emailService;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        if (await _ctx.Logins.AnyAsync(l => l.email == dto.Email))
            throw new InvalidOperationException("El correo ya está registrado");

        var login = new Login
        {
            Id           = Guid.NewGuid(),
            email        = dto.Email,
            PasswordHash = _hasher.HashPassword(null!, dto.Password),
            IsEmailLogin = true,
            IsDeleted    = false
        };

        var user = new User
        {
            Id        = Guid.NewGuid(),
            surName   = dto.SurName,
            firstName = dto.FirstName,
            cedula    = dto.Cedula,
            telefono  = dto.Telefono,
            ubicacion = dto.Ubicacion,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.Users.Add(user);
        _ctx.Logins.Add(login);
        _ctx.UserLogins.Add(new UserLogin { UserId = user.Id, LoginId = login.Id });

        foreach (var companyId in dto.CompanyIds.Distinct())
            _ctx.UserCompanies.Add(new UserCompany { UserId = user.Id, CompanyId = companyId });

        if (dto.RoleIds is not null && dto.RoleIds.Length > 0)
        {
            foreach (var companyId in dto.CompanyIds.Distinct())
            foreach (var roleId in dto.RoleIds.Distinct())
            {
                _ctx.UserRoles.Add(new UserRole
                {
                    UserId    = user.Id,
                    RoleId    = roleId,
                    CompanyId = companyId
                });
            }
        }

        await _ctx.SaveChangesAsync();

        // Enviar correo de bienvenida con credenciales (asíncrono, no bloquea)
        int? emailQueueId = null;
        bool emailQueued = false;
        
        try
        {
            var userName = $"{user.firstName} {user.surName}".Trim();
            if (string.IsNullOrWhiteSpace(userName))
                userName = dto.Email;

            // Obtener URL de la aplicación desde configuración (o usar valor por defecto)
            var applicationUrl = "https://zootecnico.sanmarino.com.co"; // Valor por defecto, puede venir de configuración
            
            // Enviar correo de forma asíncrona usando la cola (no bloquea)
            emailQueueId = await _emailService.SendWelcomeEmailAsync(
                dto.Email,
                dto.Password,
                userName,
                applicationUrl
            );
            emailQueued = emailQueueId.HasValue;
        }
        catch (Exception)
        {
            // Log del error pero no fallar el registro si el email falla
            // El usuario ya está creado, solo falló el envío del correo
        }

        var response = await GenerateResponseAsync(user, login);
        response.EmailSent = emailQueued;
        response.EmailQueueId = emailQueueId;
        
        return response;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var login = await _ctx.Logins
            .Include(l => l.UserLogins).ThenInclude(ul => ul.User)
            .FirstOrDefaultAsync(l => l.email == dto.Email && !l.IsDeleted);

        if (login is null) throw new InvalidOperationException("Credenciales inválidas");

        var userLogin = login.UserLogins.FirstOrDefault()
            ?? throw new InvalidOperationException("Usuario no relacionado");

        var user = userLogin.User;

        if (!user.IsActive || user.IsLocked || userLogin.IsLockedByAdmin)
            throw new InvalidOperationException("El usuario está bloqueado");

        var result = _hasher.VerifyHashedPassword(login, login.PasswordHash, dto.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedAttempts++;
            if (user.FailedAttempts >= 5)
            {
                user.IsLocked = true;
                user.LockedAt = DateTime.UtcNow;
            }
            await _ctx.SaveChangesAsync();
            throw new InvalidOperationException("Credenciales inválidas");
        }

        user.FailedAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();

        return await GenerateResponseAsync(user, login);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        var login = await _ctx.UserLogins
            .Include(ul => ul.Login)
            .Where(ul => ul.UserId == userId)
            .Select(ul => ul.Login)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Login no encontrado");

        var check = _hasher.VerifyHashedPassword(login, login.PasswordHash, dto.CurrentPassword);
        if (check == PasswordVerificationResult.Failed)
            throw new InvalidOperationException("Contraseña actual inválida");

        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            throw new InvalidOperationException("La nueva contraseña debe tener al menos 6 caracteres");

        login.PasswordHash = _hasher.HashPassword(login, dto.NewPassword);
        await _ctx.SaveChangesAsync();
    }

    public async Task ChangeEmailAsync(Guid userId, ChangeEmailDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewEmail))
            throw new InvalidOperationException("El correo nuevo es obligatorio");

        if (await _ctx.Logins.AnyAsync(l => l.email == dto.NewEmail))
            throw new InvalidOperationException("El correo nuevo ya está en uso");

        var login = await _ctx.UserLogins
            .Include(ul => ul.Login)
            .Where(ul => ul.UserId == userId)
            .Select(ul => ul.Login)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Login no encontrado");

        var check = _hasher.VerifyHashedPassword(login, login.PasswordHash, dto.CurrentPassword);
        if (check == PasswordVerificationResult.Failed)
            throw new InvalidOperationException("Contraseña actual inválida");

        login.email = dto.NewEmail.Trim();
        await _ctx.SaveChangesAsync();
    }

    // Genera el JWT y arma la respuesta
    private async Task<AuthResponseDto> GenerateResponseAsync(User user, Login login)
{
    // Empresas del usuario con información de país
    var userCompanies = await _ctx.UserCompanies
        .Include(uc => uc.Company)
        .Where(uc => uc.UserId == user.Id)
        .ToListAsync();

    // Roles del usuario (con CompanyId por si te interesa más tarde)
    var userRoles = await _ctx.UserRoles
        .Include(ur => ur.Role)
        .Where(ur => ur.UserId == user.Id)
        .ToListAsync();

    var roleIds = userRoles.Select(r => r.RoleId).Distinct().ToList();

    // Permisos agregados del usuario (desde sus roles)
    var permissions = await _ctx.RolePermissions
        .Include(rp => rp.Permission)
        .Where(rp => roleIds.Contains(rp.RoleId))
        .Select(rp => rp.Permission.Key)
        .Distinct()
        .ToListAsync();

    // ===== NOTA: MenusByRole también se omite del login para reducir tamaño
    // Se cargará en una segunda petición junto con el menú

    // ===== NOTA: El menú ya NO se incluye en el login para reducir el tamaño de la respuesta encriptada
    // El menú se cargará en una segunda petición separada después del login

    // ===== Claims para el JWT =====
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.UniqueName, login.email),
        new Claim(JwtRegisteredClaimNames.Email, login.email),
        new Claim("firstName", user.firstName ?? string.Empty),
        new Claim("surName",  user.surName   ?? string.Empty),
    };

    foreach (var roleName in userRoles.Select(r => r.Role?.Name)
                                      .Where(n => !string.IsNullOrWhiteSpace(n))
                                      .Distinct())
    {
        claims.Add(new Claim(ClaimTypes.Role, roleName!));
    }

    foreach (var c in userCompanies)
    {
        claims.Add(new Claim("company_id", c.CompanyId.ToString()));
        // País deshabilitado temporalmente
        var name = c.Company?.Name;
        if (!string.IsNullOrWhiteSpace(name))
            claims.Add(new Claim("company", name!));
    }

    foreach (var p in permissions.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct())
        claims.Add(new Claim("permission", p));

    // JWT
    var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
    var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var expires = DateTime.UtcNow.AddMinutes(_jwt.DurationInMinutes > 0 ? _jwt.DurationInMinutes : 120);

    var token = new JwtSecurityToken(
        issuer: _jwt.Issuer,
        audience: _jwt.Audience,
        claims: claims,
        expires: expires,
        signingCredentials: creds
    );

    // ===== Respuesta enriquecida =====
    return new AuthResponseDto
    {
        Username = login.email,
        FullName = $"{user.firstName} {user.surName}".Trim(),
        FirstName = user.firstName,
        SurName = user.surName,
        UserId   = user.Id,
        Token    = new JwtSecurityTokenHandler().WriteToken(token),

        Roles    = userRoles.Select(r => r.Role?.Name)
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Distinct()
                            .ToList()!,
        Empresas = userCompanies.Select(c => c.Company?.Name)
                                .Where(n => !string.IsNullOrWhiteSpace(n))
                                .Distinct()
                                .ToList()!,
        CompanyPaises = userCompanies.Select(uc => new CompanyPaisDto
        {
            CompanyId = uc.CompanyId,
            CompanyName = uc.Company?.Name ?? string.Empty,
            PaisId = 0,
            PaisNombre = string.Empty,
            IsDefault = uc.IsDefault
        }).ToList(),
        Permisos = permissions

        // NOTA: MenusByRole y Menu ya NO se incluyen en el login
        // Se cargarán en una segunda petición separada desde el frontend
    };
}


    // Bootstrap de sesión (usa el orquestador para menú)
    public async Task<SessionBootstrapDto> GetSessionAsync(Guid userId, int? companyId = null)
    {
        var user = await _ctx.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException("Usuario no encontrado");

        var email = await _ctx.UserLogins
            .Include(ul => ul.Login)
            .Where(ul => ul.UserId == userId)
            .Select(ul => ul.Login.email)
            .FirstOrDefaultAsync() ?? string.Empty;

        var companies = await _ctx.UserCompanies
            .Include(uc => uc.Company)
            .Where(uc => uc.UserId == user.Id)
            .Select(uc => new CompanyLiteDto(
                uc.CompanyId,
                uc.Company.Name,
                uc.Company.VisualPermissions ?? Array.Empty<string>(),
                uc.Company.MobileAccess,
                uc.Company.Identifier
            ))
            .ToListAsync();

        var rolesQuery = _ctx.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId);

        if (companyId is int cid)
            rolesQuery = rolesQuery.Where(ur => ur.CompanyId == cid);

        var roles = await rolesQuery
            .Select(ur => ur.Role.Name)
            .Where(n => n != null && n != "")
            .Distinct()
            .ToListAsync();

        var roleIds = await rolesQuery.Select(ur => ur.RoleId).Distinct().ToListAsync();

        var permissions = await _ctx.RolePermissions
            .Include(rp => rp.Permission)
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToListAsync();

        // Menú desde el orquestador (antes venía de IMenuService)
        var menu = await _acl.Menus_GetForUserAsync(userId, companyId);
        var menuList = menu.ToList();

        return new SessionBootstrapDto(
            user.Id,
            email,
            $"{user.firstName} {user.surName}".Trim(),
            user.IsActive,
            user.IsLocked,
            user.LastLoginAt,
            companyId,
            companies,
            roles,
            permissions,
            menuList
        );
    }

    // ===== Métodos para obtener menú por separado (después del login) =====
    
    public async Task<IEnumerable<MenuItemDto>> GetMenuForUserAsync(Guid userId, int? companyId = null)
    {
        // Usar el orquestador para obtener el menú del usuario (mismo método que se usaba en el login)
        return await _acl.Menus_GetForUserAsync(userId, companyId);
    }

    public async Task<List<RoleMenusLiteDto>> GetMenusByRoleForUserAsync(Guid userId)
    {
        // Obtener roles del usuario
        var userRoles = await _ctx.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .ToListAsync();

        var roleIds = userRoles.Select(r => r.RoleId).Distinct().ToList();

        // Emparejamos RoleId -> Nombre para acompañar el listado
        var rolesById = userRoles
            .Where(ur => ur.Role != null)
            .Select(ur => new { ur.RoleId, RoleName = ur.Role!.Name })
            .Distinct()
            .ToList();

        // Obtener menús asignados por rol
        var rawMenusByRole = await _ctx.RoleMenus
            .AsNoTracking()
            .Where(rm => roleIds.Contains(rm.RoleId))
            .GroupBy(rm => rm.RoleId)
            .Select(g => new
            {
                RoleId = g.Key,
                MenuIds = g.Select(x => x.MenuId).Distinct().OrderBy(x => x).ToArray()
            })
            .ToListAsync();

        var menusByRole = rawMenusByRole
            .Select(x => new RoleMenusLiteDto(
                x.RoleId,
                rolesById.FirstOrDefault(r => r.RoleId == x.RoleId)?.RoleName ?? string.Empty,
                x.MenuIds
            ))
            .OrderBy(x => x.RoleName)
            .ToList();

        return menusByRole;
    }

    public async Task<PasswordRecoveryResponseDto> RecoverPasswordAsync(PasswordRecoveryRequestDto dto)
    {
        // Validar entrada
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            throw new ArgumentException("El correo electrónico no puede estar vacío", nameof(dto.Email));
        }

        try
        {
            // Buscar el usuario por email
            var login = await _ctx.Logins
                .Include(l => l.UserLogins).ThenInclude(ul => ul.User)
                .FirstOrDefaultAsync(l => l.email == dto.Email && !l.IsDeleted);

            if (login == null)
            {
                // No loguear el email completo por seguridad, solo los primeros caracteres
                var emailMask = dto.Email.Length > 5 
                    ? dto.Email.Substring(0, 5) + "***" 
                    : "***";
                
                return new PasswordRecoveryResponseDto
                {
                    Success = false,
                    Message = "No se encontró un usuario con ese correo electrónico. Verifica que el correo esté correcto.",
                    UserFound = false,
                    EmailSent = false
                };
            }

            var user = login.UserLogins.FirstOrDefault()?.User;
            if (user == null)
            {
                return new PasswordRecoveryResponseDto
                {
                    Success = false,
                    Message = "El usuario asociado a este correo no existe en el sistema.",
                    UserFound = true,
                    EmailSent = false
                };
            }

            if (!user.IsActive)
            {
                return new PasswordRecoveryResponseDto
                {
                    Success = false,
                    Message = "Tu cuenta está inactiva. Contacta al administrador para reactivarla.",
                    UserFound = true,
                    EmailSent = false
                };
            }

            // Generar nueva contraseña aleatoria
            var newPassword = GenerateRandomPassword();

            // Actualizar la contraseña en la base de datos
            login.PasswordHash = _hasher.HashPassword(login, newPassword);
            
            try
            {
                await _ctx.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                throw new InvalidOperationException(
                    $"Error al actualizar la contraseña en la base de datos: {dbEx.Message}", 
                    dbEx);
            }

            // Enviar email con la nueva contraseña (asíncrono, no bloquea)
            int? emailQueueId = null;
            bool emailQueued = false;
            string? emailError = null;
            
            try
            {
                var userName = $"{user.firstName} {user.surName}".Trim();
                if (string.IsNullOrWhiteSpace(userName))
                    userName = null;

                // Agregar correo a la cola (no bloquea, se procesará en segundo plano)
                emailQueueId = await _emailService.SendPasswordRecoveryEmailAsync(
                    dto.Email,
                    newPassword,
                    userName
                );
                
                emailQueued = emailQueueId.HasValue;
            }
            catch (Exception emailEx)
            {
                // Log del error pero no fallar - la contraseña ya fue generada y actualizada
                // El error se registra en EmailService, pero capturamos aquí para tener contexto
                emailError = emailEx.Message;
                // No lanzamos excepción porque la contraseña ya fue actualizada exitosamente
            }

            // Siempre retornar éxito si se generó la contraseña (el correo se procesará en segundo plano)
            var message = emailQueued 
                ? "Se ha generado una nueva contraseña y se ha agregado a la cola de envío. Recibirás el correo en breve. Por favor, revisa tu bandeja de entrada y tu carpeta de spam."
                : emailError != null
                    ? $"Se ha generado una nueva contraseña. Hubo un problema al agregar el correo a la cola de envío. Contacta al administrador con el código de error: EMAIL_QUEUE_ERROR"
                    : "Se ha generado una nueva contraseña. El correo se procesará en segundo plano. Si no lo recibes en unos minutos, contacta al administrador.";

            return new PasswordRecoveryResponseDto
            {
                Success = true,
                Message = message,
                UserFound = true,
                EmailSent = emailQueued,
                EmailQueueId = emailQueueId
            };
        }
        catch (ArgumentException)
        {
            throw; // Re-lanzar para que el controlador lo maneje
        }
        catch (InvalidOperationException)
        {
            throw; // Re-lanzar para que el controlador lo maneje
        }
        catch (DbUpdateException dbEx)
        {
            throw new InvalidOperationException(
                $"Error de base de datos al recuperar contraseña: {dbEx.Message}. " +
                $"InnerException: {dbEx.InnerException?.Message}", 
                dbEx);
        }
        catch (Exception ex)
        {
            var stackTrace = ex.StackTrace ?? string.Empty;
            var stackTracePreview = stackTrace.Length > 500 
                ? stackTrace.Substring(0, 500) + "..." 
                : stackTrace;
            
            throw new InvalidOperationException(
                $"Error inesperado al recuperar contraseña: {ex.Message}. " +
                $"Tipo: {ex.GetType().Name}. " +
                $"StackTrace: {stackTracePreview}", 
                ex);
        }
    }

    private string GenerateRandomPassword(int length = 12)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
