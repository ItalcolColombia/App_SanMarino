using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de <see cref="ServiceToken"/> a la tabla <c>service_tokens</c>.
/// Los nombres de columna los resuelve EFCore.NamingConventions (snake_case) — no se fijan a mano.
/// </summary>
public class ServiceTokenConfiguration : IEntityTypeConfiguration<ServiceToken>
{
    public void Configure(EntityTypeBuilder<ServiceToken> b)
    {
        b.ToTable("service_tokens", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn();

        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.Scopes).HasMaxLength(500).IsRequired();

        b.Property(x => x.ExpiresAt);
        b.Property(x => x.RevokedAt);
        b.Property(x => x.LastUsedAt);
        b.Property(x => x.CreatedAt).IsRequired();

        // Búsqueda por hash en cada validación → único (evita colisiones y acelera el lookup).
        b.HasIndex(x => x.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_service_tokens_token_hash");

        b.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_service_tokens_user_id");
    }
}
