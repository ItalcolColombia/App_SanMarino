using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class UserFarmConfiguration : IEntityTypeConfiguration<UserFarm>
{
    public void Configure(EntityTypeBuilder<UserFarm> e)
    {
        e.ToTable("user_farms");

        // Clave compuesta
        e.HasKey(x => new { x.UserId, x.FarmId });

        // Configuración de propiedades
        e.Property(x => x.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        e.Property(x => x.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("integer")
            .IsRequired();

        e.Property(x => x.IsAdmin)
            .HasColumnName("is_admin")
            .HasColumnType("boolean")
            .HasDefaultValue(false)
            .IsRequired();

        e.Property(x => x.IsDefault)
            .HasColumnName("is_default")
            .HasColumnType("boolean")
            .HasDefaultValue(false)
            .IsRequired();

        e.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        e.Property(x => x.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasColumnType("uuid")
            .IsRequired();

        // Relaciones
        e.HasOne(x => x.User)
            .WithMany(u => u.UserFarms)
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("fk_user_farms_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Farm)
            .WithMany(f => f.UserFarms)
            .HasForeignKey(x => x.FarmId)
            .HasConstraintName("fk_user_farms_farms_farm_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Índices
        e.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_user_farms_user_id");

        e.HasIndex(x => x.FarmId)
            .HasDatabaseName("ix_user_farms_farm_id");

        e.HasIndex(x => x.IsDefault)
            .HasDatabaseName("ix_user_farms_is_default");

        // Índice único para evitar duplicados
        e.HasIndex(x => new { x.UserId, x.FarmId })
            .HasDatabaseName("ux_user_farms_user_farm")
            .IsUnique();
    }
}
