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
/// Implementación de las reglas de autorización de DB Studio. El acceso completo es doble validación:
/// el correo autorizado <b>y además</b> ser admin (rol admin/administrador, superadmin o permiso
/// <c>db_studio.admin</c>); el resto de sesiones autenticadas solo llega al resumen de migraciones.
/// Lanza <see cref="UnauthorizedAccessException"/> (→ 403) o <see cref="InvalidOperationException"/> (→ 400).
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

    /// <summary>
    /// "Admin" de DB Studio = doble validación: correo autorizado (<see cref="DbStudioMigrationCalculos.EmailConAccesoCompleto"/>)
    /// <b>y además</b> ser admin (rol admin/administrador, superadmin o permiso <c>db_studio.admin</c>).
    /// Sin el correo ni se consultan los roles. Cualquier otra sesión solo ve el resumen de migraciones.
    /// </summary>
    public async Task<bool> IsAdminAsync(CancellationToken ct = default)
    {
        if (_isAdminCache.HasValue) return _isAdminCache.Value;

        var email = _http.HttpContext?.User.FindFirstValue(ClaimTypes.Email)
                    ?? _http.HttpContext?.User.FindFirstValue("email");

        // Sin el correo autorizado no hay acceso completo — y ni siquiera hace falta mirar los roles.
        if (!DbStudioMigrationCalculos.EsCorreoAutorizado(email))
        {
            _isAdminCache = false;
            return false;
        }

        var tienePermisoDbStudioAdmin = _current.Permissions.Contains("db_studio.admin");
        IEnumerable<string> roleNames = Array.Empty<string>();
        var esSuperAdmin = false;
        if (_current.UserGuid is { } guid)
        {
            roleNames = await _ctx.UserRoles.AsNoTracking()
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == guid)
                .Select(ur => ur.Role!.Name)
                .ToListAsync(ct);
            esSuperAdmin = await SuperAdminLookup.EsSuperAdminAsync(_ctx, guid, ct);
        }

        _isAdminCache = DbStudioMigrationCalculos.TieneAccesoCompleto(
            roleNames, email, esSuperAdmin, tienePermisoDbStudioAdmin);
        return _isAdminCache.Value;
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
