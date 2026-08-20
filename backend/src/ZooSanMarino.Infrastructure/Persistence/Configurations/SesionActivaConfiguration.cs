using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de <see cref="SesionActiva"/> a la tabla <c>sesiones_activas</c>.
/// Los nombres de columna los resuelve EFCore.NamingConventions (snake_case) — no se fijan a mano.
/// <b>Sin FK a users</b>, igual que <c>ServiceToken.UserId</c>: una FK con el <c>ON DELETE</c> mal
/// elegido convertiría el borrado de un usuario en un error de runtime al arrancar en ECS.
/// </summary>
public class SesionActivaConfiguration : IEntityTypeConfiguration<SesionActiva>
{
    public void Configure(EntityTypeBuilder<SesionActiva> b)
    {
        b.ToTable("sesiones_activas", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn();

        b.Property(x => x.Jti).IsRequired();
        b.Property(x => x.UserId).IsRequired();

        b.Property(x => x.DeviceId).HasMaxLength(100);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(300);

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.ExpiresAt).IsRequired();
        b.Property(x => x.LastSeenAt);
        b.Property(x => x.RevokedAt);
        b.Property(x => x.RevokedByUserId);
        b.Property(x => x.RevokedReason).HasMaxLength(200);

        // Se busca por jti en cada request → único (además impide dos filas para el mismo token).
        b.HasIndex(x => x.Jti)
            .IsUnique()
            .HasDatabaseName("ux_sesiones_activas_jti");

        b.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_sesiones_activas_user_id");

        // El listado de la UI y la limpieza sólo miran sesiones vivas.
        b.HasIndex(x => new { x.UserId, x.ExpiresAt })
            .HasDatabaseName("ix_sesiones_activas_vivas")
            .HasFilter("revoked_at IS NULL");
    }
}
