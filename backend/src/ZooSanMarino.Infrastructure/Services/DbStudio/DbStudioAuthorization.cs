using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.DbStudio;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Implementación de las reglas de autorización de DB Studio. Centraliza la detección de acceso
/// completo (rol admin/administrador, superadmin, permiso <c>db_studio.admin</c> o correo autorizado);
/// el resto de sesiones autenticadas solo llega al resumen de migraciones. Lanza
/// <see cref="UnauthorizedAccessException"/> (→ 403) o <see cref="InvalidOperationException"/> (→ 400).
/// </summary>
public sealed class DbStudioAuthorization : IDbStudioAuthorization
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly DbStudioOptions _opts;
    private readonly IHttpContextAccessor _http;

    private bool? _isAdminCache;

    public DbStudioAuthorization(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        IOptions<DbStudioOptions> opts,
        IHttpContextAccessor http)
    {
        _ctx = ctx;
        _current = current;
        _opts = opts.Value;
        _http = http;
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
        var email = _http.HttpContext?.User.FindFirstValue(ClaimTypes.Email)
                    ?? _http.HttpContext?.User.FindFirstValue("email");
        if (DbStudioMigrationCalculos.TieneAccesoCompleto(Array.Empty<string>(), email) ||
            _current.Permissions.Contains("db_studio.admin"))
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

            result = DbStudioMigrationCalculos.TieneAccesoCompleto(roleNames, email) ||
                roleNames.Any(r => !string.IsNullOrWhiteSpace(r) &&
                    r.Equals("administrador", StringComparison.OrdinalIgnoreCase));

            if (!result)
            {
                result = await SuperAdminLookup.EsSuperAdminAsync(_ctx, guid, ct);
            }
        }

        _isAdminCache = result;
        return result;
    }

    public async Task EnsureFullAccessAsync(CancellationToken ct = default)
    {
        EnsureEnabled();
        if (!await IsAdminAsync(ct))
            throw new UnauthorizedAccessException("Esta operación requiere acceso completo a DB Studio.");
    }

    public Task EnsureMigrationSummaryAccessAsync(CancellationToken ct = default)
    {
        EnsureEnabled();
        RequireUserGuid();
        return Task.CompletedTask;
    }

    public async Task EnsureAdminAsync(CancellationToken ct = default)
    {
        EnsureEnabled();
        if (!await IsAdminAsync(ct))
            throw new UnauthorizedAccessException("Esta operación requiere rol administrador en DB Studio.");
    }

    public async Task EnsureCanReadAsync(string schema, string objectName, CancellationToken ct = default)
    {
        await EnsureFullAccessAsync(ct);
    }

    public async Task EnsureCanWriteDataAsync(string schema, string objectName, CancellationToken ct = default)
    {
        await EnsureFullAccessAsync(ct);
    }

    public async Task<HashSet<string>?> GetReadableObjectKeysAsync(CancellationToken ct = default)
    {
        await EnsureFullAccessAsync(ct);
        return null;
    }

    public async Task<MyAccessDto> GetMyAccessAsync(CancellationToken ct = default)
    {
        await EnsureFullAccessAsync(ct);
        return new MyAccessDto { IsAdmin = true };
    }
}
