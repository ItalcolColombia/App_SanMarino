using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Auth;

/// <summary>
/// Handler del esquema "ServiceToken" (PAT). Autentica requests con header
/// <c>Authorization: Bearer sk_...</c>. Valida el token contra la BD, restringe el alcance a
/// <c>/api/tickets/**</c> (+ <c>/api/auth/ping</c>) y produce el MISMO ClaimsPrincipal que produciría
/// el JWT del usuario dueño (los que consume <see cref="ICurrentUser"/>), + claim <c>token_type=service</c>.
/// </summary>
public sealed class ServiceTokenAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>Nombre del esquema; referenciado desde el policy scheme "Smart" en Program.cs.</summary>
    public const string SchemeName = "ServiceToken";

    private readonly IServiceTokenService _tokens;
    private readonly ZooSanMarinoContext _ctx;

    public ServiceTokenAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IServiceTokenService tokens,
        ZooSanMarinoContext ctx)
        : base(options, logger, encoder)
    {
        _tokens = tokens;
        _ctx = ctx;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1) Extraer "sk_..." del header Authorization.
        var authHeader = Context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader))
            return AuthenticateResult.NoResult();

        const string bearer = "Bearer ";
        var raw = authHeader.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)
            ? authHeader[bearer.Length..].Trim()
            : authHeader.Trim();

        if (!raw.StartsWith(ServiceTokenHasher.Prefix, StringComparison.Ordinal))
            return AuthenticateResult.NoResult();

        // 2) Restringir alcance de ruta ANTES de validar (defensa en profundidad).
        if (!EstaEnAlcance(Context.Request.Path))
            return AuthenticateResult.Fail("Service token fuera de alcance");

        // 3) Validar contra BD (hash, no revocado, no expirado). Actualiza LastUsedAt.
        var token = await _tokens.ValidateAsync(raw, Context.RequestAborted);
        if (token is null)
            return AuthenticateResult.Fail("Service token inválido, revocado o expirado");

        // 4) Cargar el usuario dueño y construir los MISMOS claims que su JWT.
        var claims = await BuildOwnerClaimsAsync(token.UserId, Context.RequestAborted);
        if (claims is null)
            return AuthenticateResult.Fail("El usuario dueño del token no existe o está inactivo");

        claims.Add(new Claim("token_type", "service"));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }

    /// <summary>
    /// Alcance del PAT: solo tickets. Se permite /api/auth/ping como health-check ligero del cron.
    /// </summary>
    private static bool EstaEnAlcance(PathString path) =>
        path.StartsWithSegments("/api/tickets", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/auth/ping", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Replica EXACTAMENTE el set de claims que arma AuthService.GenerateResponseAsync para el JWT:
    /// NameIdentifier/sub (guid), unique_name/email, firstName, surName, user_id (hash del guid),
    /// role (por rol), company_id + company (por empresa) y permission (agregados de roles).
    /// </summary>
    private async Task<List<Claim>?> BuildOwnerClaimsAsync(Guid userId, CancellationToken ct)
    {
        var user = await _ctx.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
        if (user is null)
            return null;

        var email = await _ctx.UserLogins.AsNoTracking()
            .Where(ul => ul.UserId == userId)
            .Select(ul => ul.Login.email)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var userCompanies = await _ctx.UserCompanies.AsNoTracking()
            .Include(uc => uc.Company)
            .Where(uc => uc.UserId == userId)
            .ToListAsync(ct);

        var userRoles = await _ctx.UserRoles.AsNoTracking()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .ToListAsync(ct);

        var roleIds = userRoles.Select(r => r.RoleId).Distinct().ToList();

        var permissions = await _ctx.RolePermissions.AsNoTracking()
            .Include(rp => rp.Permission)
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToListAsync(ct);

        // Mismo cálculo que el JWT (identificador numérico de compatibilidad).
        var userIdHash = Math.Abs(user.Id.GetHashCode());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, email),
            new(JwtRegisteredClaimNames.Email, email),
            new("firstName", user.firstName ?? string.Empty),
            new("surName",  user.surName   ?? string.Empty),
            new("user_id", userIdHash.ToString()),
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

        return claims;
    }
}
