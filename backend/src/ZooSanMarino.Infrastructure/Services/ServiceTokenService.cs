using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Emisión / revocación / validación de tokens de servicio (PAT).
/// El hashing/generación es lógica pura (ServiceTokenHasher); aquí solo va la persistencia.
/// </summary>
public class ServiceTokenService : IServiceTokenService
{
    private readonly ZooSanMarinoContext _ctx;

    public ServiceTokenService(ZooSanMarinoContext ctx) => _ctx = ctx;

    public async Task<(string PlainToken, ServiceTokenDto Dto)> IssueAsync(
        string name, Guid ownerUserId, string scopes, DateTime? expiresAt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("El nombre del token es obligatorio.");

        // El usuario dueño debe existir: sus claims (roles/company/permisos) se replican al autenticar.
        var ownerExists = await _ctx.Users.AsNoTracking().AnyAsync(u => u.Id == ownerUserId, ct);
        if (!ownerExists)
            throw new InvalidOperationException("El usuario dueño del token no existe.");

        var plain = ServiceTokenHasher.GenerateToken();

        var entity = new ServiceToken
        {
            Name       = name.Trim(),
            TokenHash  = ServiceTokenHasher.Hash(plain), // solo el hash se persiste
            UserId     = ownerUserId,
            Scopes     = (scopes ?? string.Empty).Trim(),
            ExpiresAt  = expiresAt,
            CreatedAt  = DateTime.UtcNow
        };

        _ctx.ServiceTokens.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return (plain, ToDto(entity));
    }

    public async Task<bool> RevokeAsync(long id, CancellationToken ct)
    {
        var entity = await _ctx.ServiceTokens.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null || entity.RevokedAt is not null)
            return false;

        entity.RevokedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ServiceToken?> ValidateAsync(string plainToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plainToken) ||
            !plainToken.StartsWith(ServiceTokenHasher.Prefix, StringComparison.Ordinal))
            return null;

        var hash = ServiceTokenHasher.Hash(plainToken);

        var entity = await _ctx.ServiceTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (entity is null)
            return null;

        // Revocado o expirado → inválido.
        if (entity.RevokedAt is not null)
            return null;
        if (entity.ExpiresAt is DateTime exp && exp <= DateTime.UtcNow)
            return null;

        entity.LastUsedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        return entity;
    }

    private static ServiceTokenDto ToDto(ServiceToken t) => new(
        t.Id, t.Name, t.Scopes, t.ExpiresAt, t.RevokedAt, t.LastUsedAt, t.CreatedAt);
}
