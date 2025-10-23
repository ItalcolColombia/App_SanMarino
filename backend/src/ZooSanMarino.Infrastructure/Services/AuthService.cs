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
    // private readonly IEmailService _emailService; // Temporalmente comentado para debug

    public AuthService(
        ZooSanMarinoContext ctx,
        IPasswordHasher<Login> hasher,
        JwtOptions jwt,
        IRoleCompositeService acl) // ← inyectamos el orquestador
        // IEmailService emailService) // Temporalmente comentado para debug
    {
        _ctx = ctx;
        _hasher = hasher;
        _jwt = jwt;
        _acl = acl;
        // _emailService = emailService; // Temporalmente comentado para debug
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
        return await GenerateResponseAsync(user, login);
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
    // Empresas del usuario
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

    // ===== NUEVO: Menús asignados por rol (ids) =====
    // Emparejamos RoleId -> Nombre para acompañar el listado
    var rolesById = userRoles
        .Where(ur => ur.Role != null)
        .Select(ur => new { ur.RoleId, RoleName = ur.Role!.Name })
        .Distinct()
        .ToList();

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
        .OrderBy(x => x.RoleName) // opcional: por orden alfabético
        .ToList();

    // ===== NUEVO: Menú efectivo del usuario (árbol) =====
    // Usa el orquestador para calcular el árbol según permisos del usuario.
    // Si quieres que dependa de una empresa concreta, pásala como companyId.
    var effectiveMenu = await _acl.Menus_GetForUserAsync(user.Id, companyId: null);
    var effectiveMenuList = effectiveMenu.ToList();

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
        Permisos = permissions,

        // NUEVO
        MenusByRole = menusByRole,
        Menu        = effectiveMenuList
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

    // Método temporalmente comentado para debug
    /*
    public async Task<PasswordRecoveryResponseDto> RecoverPasswordAsync(PasswordRecoveryRequestDto dto)
    {
        try
        {
            // Buscar el usuario por email
            var login = await _ctx.Logins
                .Include(l => l.UserLogins).ThenInclude(ul => ul.User)
                .FirstOrDefaultAsync(l => l.email == dto.Email && !l.IsDeleted);

            if (login == null)
            {
                return new PasswordRecoveryResponseDto
                {
                    Success = false,
                    Message = "No se encontró un usuario con ese correo electrónico",
                    UserFound = false,
                    EmailSent = false
                };
            }

            var user = login.UserLogins.FirstOrDefault()?.User;
            if (user == null || !user.IsActive)
            {
                return new PasswordRecoveryResponseDto
                {
                    Success = false,
                    Message = "El usuario está inactivo o no existe",
                    UserFound = true,
                    EmailSent = false
                };
            }

            // Generar nueva contraseña aleatoria
            var newPassword = GenerateRandomPassword();

            // Actualizar la contraseña en la base de datos
            login.PasswordHash = _hasher.HashPassword(login, newPassword);
            await _ctx.SaveChangesAsync();

            // Enviar email con la nueva contraseña
            try
            {
                await _emailService.SendPasswordRecoveryEmailAsync(dto.Email, newPassword);
                
                return new PasswordRecoveryResponseDto
                {
                    Success = true,
                    Message = "Se ha enviado una nueva contraseña a tu correo electrónico",
                    UserFound = true,
                    EmailSent = true
                };
            }
            catch (Exception)
            {
                // Si falla el envío del email, revertir el cambio de contraseña
                // (opcional: podrías mantener la nueva contraseña y solo reportar el error)
                return new PasswordRecoveryResponseDto
                {
                    Success = false,
                    Message = "Se generó una nueva contraseña pero hubo un error al enviar el email. Contacta al administrador.",
                    UserFound = true,
                    EmailSent = false
                };
            }
        }
        catch (Exception)
        {
            return new PasswordRecoveryResponseDto
            {
                Success = false,
                Message = "Ocurrió un error interno. Intenta nuevamente más tarde.",
                UserFound = false,
                EmailSent = false
            };
        }
    }
    */

    private string GenerateRandomPassword(int length = 12)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
