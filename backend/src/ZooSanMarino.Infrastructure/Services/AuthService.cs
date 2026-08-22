// src/ZooSanMarino.Infrastructure/Services/AuthService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ZooSanMarino.Application.Calculos;
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

    // B1 — revocación de sesión. El login deja de emitir un token irrevocable: registra su `jti`
    // en `sesiones_activas` (lista blanca), y cambiar la contraseña apaga todas las sesiones vivas.
    private readonly ISesionActivaService _sesiones;
    private readonly IHttpContextAccessor _http;

    // Respuesta única para recuperación de contraseña: no revela si el correo existe (anti-enumeración).
    private const string NeutralRecoveryMessage =
        "Si el correo está registrado, recibirás un mensaje con instrucciones para restablecer tu contraseña.";

    public AuthService(
        ZooSanMarinoContext ctx,
        IPasswordHasher<Login> hasher,
        JwtOptions jwt,
        IRoleCompositeService acl,
        IEmailService emailService,
        ISesionActivaService sesiones,
        IHttpContextAccessor http)
    {
        _ctx = ctx;
        _hasher = hasher;
        _jwt = jwt;
        _acl = acl;
        _emailService = emailService;
        _sesiones = sesiones;
        _http = http;
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
            IsEmailLogin = !dto.IsPlatformUser,
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
            Zona      = string.IsNullOrWhiteSpace(dto.Zona) ? null : dto.Zona.Trim(),
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

        // Enviar correo de bienvenida solo si es un usuario con email real
        int? emailQueueId = null;
        bool emailQueued = false;

        if (!dto.IsPlatformUser)
        {
            try
            {
                var userName = $"{user.firstName} {user.surName}".Trim();
                if (string.IsNullOrWhiteSpace(userName))
                    userName = dto.Email;

                var applicationUrl = "https://zootecnico.sanmarino.com.co";

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
                // No fallar el registro si el email falla
            }
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

        // Bloqueo administrativo o cuenta inactiva: siempre cerrado.
        if (!user.IsActive || userLogin.IsLockedByAdmin)
            throw new InvalidOperationException("El usuario está bloqueado");

        // Bloqueo por intentos fallidos: TEMPORAL. Se auto-desbloquea tras LockoutMinutes
        // para no convertir el login en un vector de DoS de cuenta.
        const int LockoutMinutes = 15;
        if (user.IsLocked)
        {
            var lockedSince = user.LockedAt ?? DateTime.UtcNow;
            if (DateTime.UtcNow - lockedSince < TimeSpan.FromMinutes(LockoutMinutes))
                throw new InvalidOperationException("El usuario está bloqueado temporalmente. Intenta más tarde.");

            // Expiró el bloqueo → reabrir e iniciar de cero.
            user.IsLocked = false;
            user.LockedAt = null;
            user.FailedAttempts = 0;
        }

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

        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
            throw new InvalidOperationException("La nueva contraseña debe tener al menos 8 caracteres");

        login.PasswordHash = _hasher.HashPassword(login, dto.NewPassword);
        await _ctx.SaveChangesAsync();

        // B1 — cambiar la contraseña apaga TODAS las sesiones del usuario, incluida la actual.
        // Hasta ago-2026 no invalidaba nada: quien se hubiera llevado un token seguía entrando con
        // él hasta que venciera, aunque la víctima cambiara la clave justo por eso.
        await _sesiones.RevocarTodasDelUsuarioAsync(
            userId, revocadaPor: userId, motivo: "Cambio de contraseña", CancellationToken.None);
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

    /// <summary>
    /// Permisos efectivos del usuario: los que dan sus roles, intersectados con los que habilita la
    /// EMPRESA de cada rol (<c>company_permissions</c>).
    /// <para>
    /// El par (rol, empresa) importa: el mismo permiso puede llegar por un rol de una empresa que lo
    /// habilita y por otro de una que no — sobrevive por el primero, una sola vez. Por eso no se
    /// resuelve con la "empresa activa" sino con la empresa de cada <c>user_roles</c>.
    /// </para>
    /// <para>
    /// Es FAIL-CLOSED: una empresa sin ninguna fila en <c>company_permissions</c> no aporta permisos.
    /// El seed inicial cubre todas las empresas existentes y <c>CompanyService.CreateAsync</c> siembra
    /// el catálogo completo en cada empresa nueva, así que ese caso no debería darse en la práctica.
    /// </para>
    /// </summary>
    private async Task<List<string>> PermisosEfectivosAsync(IEnumerable<(int CompanyId, int RoleId)> paresRolEmpresa)
    {
        var pares = paresRolEmpresa.Distinct().ToList();
        if (pares.Count == 0) return new List<string>();

        var roleIds = pares.Select(p => p.RoleId).Distinct().ToList();
        var companyIds = pares.Select(p => p.CompanyId).Distinct().ToList();

        var keysPorRol = (await _ctx.RolePermissions
                .AsNoTracking()
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Select(rp => new { rp.RoleId, rp.Permission.Key })
                .ToListAsync())
            .GroupBy(x => x.RoleId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyCollection<string>)g.Select(x => x.Key).Distinct().ToList());

        var habilitadasPorEmpresa = (await _ctx.CompanyPermissions
                .AsNoTracking()
                .Where(cp => companyIds.Contains(cp.CompanyId) && cp.IsEnabled)
                .Select(cp => new { cp.CompanyId, cp.Permission.Key })
                .ToListAsync())
            .GroupBy(x => x.CompanyId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyCollection<string>)g.Select(x => x.Key).Distinct().ToList());

        var entrada = pares
            .Where(p => keysPorRol.ContainsKey(p.RoleId))
            .Select(p => (p.CompanyId, keysPorRol[p.RoleId]));

        return CompanyPermissionCalculos
            .ResolverEfectivos(entrada, habilitadasPorEmpresa)
            .ToList();
    }

    /// <summary>
    /// Metadatos del dispositivo que está iniciando sesión, para poder listarla y reconocerla después
    /// («esta es la tablet del galpón 3»). Todos opcionales: sin contexto HTTP —tests, seeds— la sesión
    /// se registra igual, porque lo que la hace revocable es la fila, no su metadata.
    /// </summary>
    private (string? DeviceId, string? Ip, string? UserAgent) DatosDelDispositivo()
    {
        var ctx = _http.HttpContext;
        if (ctx is null) return (null, null, null);

        var deviceId = ctx.Request.Headers[RateLimitingCalculos.DeviceIdHeader].ToString();
        var userAgent = ctx.Request.Headers.UserAgent.ToString();
        var ip = ctx.Connection.RemoteIpAddress?.ToString();

        return (
            string.IsNullOrWhiteSpace(deviceId) ? null : deviceId,
            string.IsNullOrWhiteSpace(ip) ? null : ip,
            string.IsNullOrWhiteSpace(userAgent) ? null : userAgent);
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

    // Permisos agregados del usuario (desde sus roles), acotados por lo que habilita la empresa de
    // cada rol (company_permissions). Ver PermisosEfectivosAsync.
    var permissions = await PermisosEfectivosAsync(
        userRoles.Select(ur => (ur.CompanyId, ur.RoleId)));

    // ===== NOTA: MenusByRole también se omite del login para reducir tamaño
    // Se cargará en una segunda petición junto con el menú

    // ===== NOTA: El menú ya NO se incluye en el login para reducir el tamaño de la respuesta encriptada
    // El menú se cargará en una segunda petición separada después del login

    // ===== Claims para el JWT =====
    // Generar un identificador numérico único desde el Guid para compatibilidad
    var userIdHash = Math.Abs(user.Id.GetHashCode());

    // B1 — identidad de ESTA sesión. Se genera ANTES del token porque hay que persistirla:
    // el `jti` es la llave con la que cada request pregunta si la sesión sigue viva.
    var jti = Guid.NewGuid();
    var emitidoEn = DateTime.UtcNow;

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Jti, jti.ToString()),
        new Claim(JwtRegisteredClaimNames.Iat,
            EpochTime.GetIntDate(emitidoEn).ToString(), ClaimValueTypes.Integer64),
        new Claim(JwtRegisteredClaimNames.UniqueName, login.email),
        new Claim(JwtRegisteredClaimNames.Email, login.email),
        new Claim("firstName", user.firstName ?? string.Empty),
        new Claim("surName",  user.surName   ?? string.Empty),
        new Claim("user_id", userIdHash.ToString()), // Identificador numérico para compatibilidad
        // Sale del DATO (`users.is_super_admin`), no del correo. Lo lee `GET /auth/profile`, que
        // sólo hace eco de la sesión. Las 12 compuertas REALES de autorización no usan el claim:
        // consultan la columna en cada request (ver SuperAdminLookup), así que revocar la marca
        // tiene efecto inmediato aunque el token siga vivo.
        new Claim("is_super_admin", SuperAdminCalculos.EsSuperAdmin(user.IsSuperAdmin) ? "true" : "false"),
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

    // B1 — el token recién emitido queda anotado como sesión viva. Sin esta fila el token no vale:
    // es una lista BLANCA, no una lista negra. Así una tablet perdida se apaga con un UPDATE en vez
    // de esperar a que venza el token.
    var (deviceId, ip, userAgent) = DatosDelDispositivo();
    await _sesiones.RegistrarAsync(jti, user.Id, expires, deviceId, ip, userAgent, CancellationToken.None);

    // ===== Respuesta enriquecida =====
    // Obtener IDs de empresas del usuario
    var userCompanyIds = userCompanies.Select(uc => uc.CompanyId).ToList();
    
    // Consultar países asociados a cada empresa del usuario desde CompanyPaises
    var companyPaisesFromDb = await _ctx.CompanyPaises
        .Include(cp => cp.Company)
            .ThenInclude(c => c.Logo)
        .Include(cp => cp.Pais)
        .Where(cp => userCompanyIds.Contains(cp.CompanyId))
        .ToListAsync();

    // Crear diccionario para saber si una empresa es default
    var defaultCompanies = userCompanies
        .Where(uc => uc.IsDefault)
        .Select(uc => uc.CompanyId)
        .ToHashSet();

    // Crear lista de CompanyPaisDto con información completa de país
    static string? BuildCompanyLogoDataUrl(Company? c)
    {
        var logo = c?.Logo;
        if (logo?.LogoBytes == null || logo.LogoBytes.Length == 0) return null;
        var ct = string.IsNullOrWhiteSpace(logo.LogoContentType) ? "image/png" : logo.LogoContentType.Trim();
        return $"data:{ct};base64,{Convert.ToBase64String(logo.LogoBytes)}";
    }

    var companyPaisesList = companyPaisesFromDb.Select(cp => new CompanyPaisDto
    {
        CompanyId = cp.CompanyId,
        CompanyName = cp.Company?.Name ?? string.Empty,
        CompanyLogoDataUrl = BuildCompanyLogoDataUrl(cp.Company),
        PaisId = cp.PaisId,
        PaisNombre = cp.Pais?.PaisNombre ?? string.Empty,
        IsDefault = defaultCompanies.Contains(cp.CompanyId),
        DescuentaInventarioDesdeMovil = cp.Company?.DescuentaInventarioDesdeMovil ?? false
    }).ToList();
    
    // Si una empresa no tiene países asociados, agregarla sin país (para compatibilidad)
    var companiesWithPaises = companyPaisesList.Select(cp => cp.CompanyId).Distinct().ToHashSet();
    foreach (var uc in userCompanies)
    {
        if (!companiesWithPaises.Contains(uc.CompanyId))
        {
            companyPaisesList.Add(new CompanyPaisDto
            {
                CompanyId = uc.CompanyId,
                CompanyName = uc.Company?.Name ?? string.Empty,
                CompanyLogoDataUrl = BuildCompanyLogoDataUrl(uc.Company),
                PaisId = 0,
                PaisNombre = string.Empty,
                IsDefault = uc.IsDefault,
                DescuentaInventarioDesdeMovil = uc.Company?.DescuentaInventarioDesdeMovil ?? false
            });
        }
    }

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
        CompanyPaises = companyPaisesList,
        Permisos = permissions,

        // Super Admin (Admin General): gatea funciones exclusivas en el front (ej. flag Admin de
        // Empresa en Roles). Sale del DATO del usuario (`users.is_super_admin`), no de su correo:
        // ver SuperAdminCalculos. El nombre y la semántica del campo no cambian.
        IsSuperAdmin = SuperAdminCalculos.EsSuperAdmin(user.IsSuperAdmin)

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

        // Mismo gate por empresa que en el login (ver PermisosEfectivosAsync). Acá los pares ya
        // vienen filtrados por companyId cuando el llamador lo pasa.
        var paresRolEmpresa = await rolesQuery
            .Select(ur => new { ur.CompanyId, ur.RoleId })
            .Distinct()
            .ToListAsync();

        var permissions = await PermisosEfectivosAsync(
            paresRolEmpresa.Select(p => (p.CompanyId, p.RoleId)));

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
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("El correo electrónico no puede estar vacío", nameof(dto.Email));

        try
        {
            var login = await _ctx.Logins
                .Include(l => l.UserLogins).ThenInclude(ul => ul.User)
                .FirstOrDefaultAsync(l => l.email == dto.Email && !l.IsDeleted);

            var user = login?.UserLogins.FirstOrDefault()?.User;
            if (login == null || user == null || !user.IsActive)
            {
                return new PasswordRecoveryResponseDto
                {
                    Success = true,
                    Message = NeutralRecoveryMessage,
                    UserFound = false,
                    EmailSent = false
                };
            }

            // Generar token único (CSPRNG-based, válido por 15 minutos)
            var resetToken = GeneratePasswordResetToken();
            var expiresAt = DateTime.UtcNow.AddMinutes(15);

            var passwordResetToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = resetToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsUsed = false
            };

            // Invalidar tokens anteriores para este usuario
            var oldTokens = await _ctx.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.IsUsed)
                .ToListAsync();

            foreach (var oldToken in oldTokens)
                oldToken.IsUsed = true;

            _ctx.PasswordResetTokens.Add(passwordResetToken);

            try
            {
                await _ctx.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                throw new InvalidOperationException("No se pudo procesar la solicitud.", dbEx);
            }

            // Enviar email con el token (link a frontend para validar)
            try
            {
                var userName = $"{user.firstName} {user.surName}".Trim();
                if (string.IsNullOrWhiteSpace(userName))
                    userName = null;

                // Enlace de un solo uso, NO el token presentado como contraseña: el correo se manda
                // con SendPasswordResetLinkEmailAsync justamente para no volver a confundirlos.
                await _emailService.SendPasswordResetLinkEmailAsync(dto.Email, resetToken, userName);
            }
            catch (Exception)
            {
                // El error de envío se registra en EmailService; aquí se ignora deliberadamente.
            }

            // Respuesta neutra: anti-enumeración
            return new PasswordRecoveryResponseDto
            {
                Success = true,
                Message = NeutralRecoveryMessage,
                UserFound = false,
                EmailSent = false
            };
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (DbUpdateException dbEx)
        {
            throw new InvalidOperationException("No se pudo procesar la solicitud.", dbEx);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("No se pudo procesar la solicitud.", ex);
        }
    }

    public async Task<ValidatePasswordResetTokenResponseDto> ValidateAndUsePasswordResetTokenAsync(ValidatePasswordResetTokenDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Token))
            throw new ArgumentException("El token es obligatorio", nameof(dto.Token));

        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
            throw new ArgumentException("La nueva contraseña debe tener al menos 8 caracteres", nameof(dto.NewPassword));

        try
        {
            var resetToken = await _ctx.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == dto.Token && !t.IsUsed);

            // Token inválido, expirado, o ya consumido
            if (resetToken == null || resetToken.ExpiresAt < DateTime.UtcNow)
            {
                return new ValidatePasswordResetTokenResponseDto
                {
                    Success = false,
                    Message = "El enlace de restablecimiento de contraseña es inválido o ha expirado."
                };
            }

            // Marcar token como consumido
            resetToken.IsUsed = true;
            resetToken.UsedAt = DateTime.UtcNow;

            // Obtener el login del usuario
            var userLogin = await _ctx.UserLogins
                .Include(ul => ul.Login)
                .FirstOrDefaultAsync(ul => ul.UserId == resetToken.UserId);

            if (userLogin?.Login == null)
            {
                return new ValidatePasswordResetTokenResponseDto
                {
                    Success = false,
                    Message = "No se pudo procesar la solicitud."
                };
            }

            // Actualizar contraseña
            userLogin.Login.PasswordHash = _hasher.HashPassword(userLogin.Login, dto.NewPassword);

            try
            {
                await _ctx.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                throw new InvalidOperationException("No se pudo procesar la solicitud.", dbEx);
            }

            // B1 — mismo criterio que los otros dos caminos de cambio de contraseña: el enlace de
            // recuperación se usa, justamente, cuando alguien perdió el control de su cuenta.
            await _sesiones.RevocarTodasDelUsuarioAsync(
                resetToken.UserId, revocadaPor: resetToken.UserId,
                motivo: "Restablecimiento de contraseña", CancellationToken.None);

            return new ValidatePasswordResetTokenResponseDto
            {
                Success = true,
                Message = "Contraseña restablecida exitosamente."
            };
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("No se pudo procesar la solicitud.", ex);
        }
    }

    public async Task<AdminResetPasswordResponseDto> AdminResetPasswordAsync(Guid userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            throw new InvalidOperationException("La nueva contraseña debe tener al menos 8 caracteres");

        var loginData = await _ctx.UserLogins
            .Include(ul => ul.Login)
            .Include(ul => ul.User)
            .Where(ul => ul.UserId == userId)
            .Select(ul => new { ul.Login, ul.User })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Usuario no encontrado");

        loginData.Login.PasswordHash = _hasher.HashPassword(loginData.Login, newPassword);
        await _ctx.SaveChangesAsync();

        // B1 — restablecer la contraseña desde administración apaga las sesiones del usuario: si se
        // hace porque perdió el equipo o le tomaron la cuenta, dejarlas vivas anula el remedio.
        await _sesiones.RevocarTodasDelUsuarioAsync(
            userId, revocadaPor: null, motivo: "Restablecimiento de contraseña por administración",
            CancellationToken.None);

        int? emailQueueId = null;
        bool emailQueued = false;

        try
        {
            var userName = $"{loginData.User.firstName} {loginData.User.surName}".Trim();
            if (string.IsNullOrWhiteSpace(userName))
                userName = loginData.Login.email;

            emailQueueId = await _emailService.SendPasswordRecoveryEmailAsync(
                loginData.Login.email,
                newPassword,
                userName
            );
            emailQueued = emailQueueId.HasValue;
        }
        catch (Exception)
        {
            // La contraseña ya fue actualizada; el correo fallará silenciosamente
        }

        return new AdminResetPasswordResponseDto
        {
            Success = true,
            Message = emailQueued
                ? "Contraseña restablecida y notificación enviada al correo del usuario."
                : "Contraseña restablecida. No se pudo enviar la notificación por correo.",
            EmailQueueId = emailQueueId,
            EmailQueued = emailQueued
        };
    }

    private static string GenerateRandomPassword(int length = 16)
    {
        // CSPRNG (no System.Random, que es predecible).
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var buffer = new char[length];
        for (int i = 0; i < length; i++)
            buffer[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        return new string(buffer);
    }

    private static string GeneratePasswordResetToken(int length = 64)
    {
        // URL-safe token: CSPRNG-generated random bytes encoded as base64url.
        var buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
