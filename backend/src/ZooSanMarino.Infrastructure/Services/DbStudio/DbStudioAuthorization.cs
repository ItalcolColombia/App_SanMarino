using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.DbStudio;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Implementación de las reglas de autorización de DB Studio. Centraliza la detección de admin
/// y el filtrado por grants. Lanza <see cref="UnauthorizedAccessException"/> (→ 403) o
/// <see cref="InvalidOperationException"/> (→ 400) según el caso.
/// </summary>
public sealed class DbStudioAuthorization : IDbStudioAuthorization
{
    private const string SuperAdminEmail = "moiesbbuga@gmail.com";

    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly DbStudioOptions _opts;

    private bool? _isAdminCache;

    public DbStudioAuthorization(ZooSanMarinoContext ctx, ICurrentUser current, IOptions<DbStudioOptions> opts)
    {
        _ctx = ctx;
        _current = current;
        _opts = opts.Value;
    }

    private void EnsureEnabled()
    {
        if (!_opts.Enabled)
            throw new InvalidOperationException("DB Studio está deshabilitado por configuración.");
    }

    private Guid RequireUserGuid()
        => _current.UserGuid ?? throw new UnauthorizedAccessException("Sesión no autenticada.");

    public async Task<bool> IsAdminAsync(CancellationToken ct = default)
    {
        if (_isAdminCache.HasValue) return _isAdminCache.Value;

        var result = false;
        if (_current.Permissions.Contains("db_studio.admin"))
        {
            result = true;
        }
        else if (_current.UserGuid is { } guid)
        {
            var roleNames = await _ctx.UserRoles.AsNoTracking()
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == guid)
                .Select(ur => ur.Role!.Name)
                .ToListAsync(ct);

            result = roleNames.Any(r => !string.IsNullOrWhiteSpace(r) &&
                (r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                 r.Equals("administrador", StringComparison.OrdinalIgnoreCase)));

            if (!result)
            {
                var email = await _ctx.UserLogins.AsNoTracking()
                    .Include(ul => ul.Login)
                    .Where(ul => ul.UserId == guid)
                    .Select(ul => ul.Login!.email)
                    .FirstOrDefaultAsync(ct);
                result = string.Equals(email, SuperAdminEmail, StringComparison.OrdinalIgnoreCase);
            }
        }

        _isAdminCache = result;
        return result;
    }

    public async Task EnsureModuleAccessAsync(CancellationToken ct = default)
    {
        EnsureEnabled();
        if (await IsAdminAsync(ct)) return;
        if (_current.Permissions.Contains("db_studio.access")) { RequireUserGuid(); return; }
        throw new UnauthorizedAccessException("No tenés acceso a DB Studio.");
    }

    public async Task EnsureAdminAsync(CancellationToken ct = default)
    {
        EnsureEnabled();
        if (!await IsAdminAsync(ct))
            throw new UnauthorizedAccessException("Esta operación requiere rol administrador en DB Studio.");
    }

    public async Task EnsureCanReadAsync(string schema, string objectName, CancellationToken ct = default)
    {
        EnsureEnabled();
        if (await IsAdminAsync(ct)) return;
        var guid = RequireUserGuid();
        var ok = await _ctx.DbStudioObjectGrants.AsNoTracking().AnyAsync(g =>
            g.UserId == guid && g.CompanyId == _current.CompanyId &&
            g.SchemaName == schema && g.ObjectName == objectName, ct);
        if (!ok)
            throw new UnauthorizedAccessException($"No tenés permiso de lectura sobre {schema}.{objectName}.");
    }

    public async Task EnsureCanWriteDataAsync(string schema, string objectName, CancellationToken ct = default)
    {
        EnsureEnabled();
        if (await IsAdminAsync(ct)) return;
        var guid = RequireUserGuid();
        var ok = await _ctx.DbStudioObjectGrants.AsNoTracking().AnyAsync(g =>
            g.UserId == guid && g.CompanyId == _current.CompanyId &&
            g.SchemaName == schema && g.ObjectName == objectName &&
            g.AccessLevel == DbStudioAccessLevel.Write, ct);
        if (!ok)
            throw new UnauthorizedAccessException($"No tenés permiso de escritura sobre {schema}.{objectName}.");
    }

    public async Task<HashSet<string>?> GetReadableObjectKeysAsync(CancellationToken ct = default)
    {
        if (await IsAdminAsync(ct)) return null; // admin = todos
        var guid = RequireUserGuid();
        var keys = await _ctx.DbStudioObjectGrants.AsNoTracking()
            .Where(g => g.UserId == guid && g.CompanyId == _current.CompanyId)
            .Select(g => (g.SchemaName + "." + g.ObjectName).ToLower())
            .ToListAsync(ct);
        return keys.ToHashSet();
    }

    public async Task<MyAccessDto> GetMyAccessAsync(CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(ct);
        var dto = new MyAccessDto { IsAdmin = isAdmin };
        if (isAdmin) return dto;

        var guid = RequireUserGuid();
        dto.Objects = await _ctx.DbStudioObjectGrants.AsNoTracking()
            .Where(g => g.UserId == guid && g.CompanyId == _current.CompanyId)
            .Select(g => new MyAccessItemDto
            {
                Schema = g.SchemaName,
                Object = g.ObjectName,
                AccessLevel = g.AccessLevel == DbStudioAccessLevel.Write ? "write" : "read"
            })
            .ToListAsync(ct);
        return dto;
    }
}
