using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>Gestión de grants por objeto (admin). Scope por empresa activa del actor.</summary>
public sealed class DbStudioPermissionService : IDbStudioPermissionService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;

    public DbStudioPermissionService(ZooSanMarinoContext ctx, ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    private static string LevelStr(DbStudioAccessLevel l) => l == DbStudioAccessLevel.Write ? "write" : "read";
    private static DbStudioAccessLevel ParseLevel(string? s) =>
        string.Equals(s, "write", StringComparison.OrdinalIgnoreCase) ? DbStudioAccessLevel.Write : DbStudioAccessLevel.Read;

    private static ObjectGrantDto ToDto(DbStudioObjectGrant g) => new()
    {
        Id = g.Id,
        UserId = g.UserId,
        CompanyId = g.CompanyId,
        Schema = g.SchemaName,
        Object = g.ObjectName,
        AccessLevel = LevelStr(g.AccessLevel),
        GrantedByUserId = g.GrantedByUserId,
        GrantedAtUtc = g.GrantedAtUtc
    };

    public async Task<IEnumerable<ObjectGrantDto>> GetGrantsByUserAsync(Guid userId, CancellationToken ct = default)
        => await _ctx.DbStudioObjectGrants.AsNoTracking()
            .Where(g => g.UserId == userId && g.CompanyId == _current.CompanyId)
            .OrderBy(g => g.SchemaName).ThenBy(g => g.ObjectName)
            .Select(g => ToDto(g)).ToListAsync(ct);

    public async Task<IEnumerable<ObjectGrantDto>> GetAllGrantsAsync(CancellationToken ct = default)
        => await _ctx.DbStudioObjectGrants.AsNoTracking()
            .Where(g => g.CompanyId == _current.CompanyId)
            .OrderBy(g => g.UserId).ThenBy(g => g.SchemaName).ThenBy(g => g.ObjectName)
            .Select(g => ToDto(g)).ToListAsync(ct);

    public async Task<ObjectGrantDto> UpsertGrantAsync(GrantRequest request, CancellationToken ct = default)
    {
        if (request.UserId == Guid.Empty) throw new InvalidOperationException("UserId requerido.");
        if (string.IsNullOrWhiteSpace(request.Object)) throw new InvalidOperationException("Objeto requerido.");
        var schema = string.IsNullOrWhiteSpace(request.Schema) ? "public" : request.Schema;

        var existing = await _ctx.DbStudioObjectGrants.FirstOrDefaultAsync(g =>
            g.UserId == request.UserId && g.CompanyId == _current.CompanyId &&
            g.SchemaName == schema && g.ObjectName == request.Object, ct);

        if (existing is null)
        {
            existing = new DbStudioObjectGrant
            {
                UserId = request.UserId,
                CompanyId = _current.CompanyId,
                SchemaName = schema,
                ObjectName = request.Object,
                AccessLevel = ParseLevel(request.AccessLevel),
                GrantedByUserId = _current.UserGuid ?? Guid.Empty,
                GrantedAtUtc = DateTime.UtcNow
            };
            _ctx.DbStudioObjectGrants.Add(existing);
        }
        else
        {
            existing.AccessLevel = ParseLevel(request.AccessLevel);
            existing.GrantedByUserId = _current.UserGuid ?? Guid.Empty;
            existing.GrantedAtUtc = DateTime.UtcNow;
        }
        await _ctx.SaveChangesAsync(ct);
        return ToDto(existing);
    }

    public async Task RevokeGrantAsync(long grantId, CancellationToken ct = default)
    {
        var g = await _ctx.DbStudioObjectGrants.FirstOrDefaultAsync(
            x => x.Id == grantId && x.CompanyId == _current.CompanyId, ct);
        if (g is null) return;
        _ctx.DbStudioObjectGrants.Remove(g);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task RevokeGrantAsync(Guid userId, string schema, string objectName, CancellationToken ct = default)
    {
        var g = await _ctx.DbStudioObjectGrants.FirstOrDefaultAsync(x =>
            x.UserId == userId && x.CompanyId == _current.CompanyId &&
            x.SchemaName == schema && x.ObjectName == objectName, ct);
        if (g is null) return;
        _ctx.DbStudioObjectGrants.Remove(g);
        await _ctx.SaveChangesAsync(ct);
    }
}
