// src/ZooSanMarino.Infrastructure/Persistence/Configurations/UserConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> e)
        {
            e.ToTable("users");

            // Clave primaria
            e.HasKey(u => u.Id);

            // UUID generado por Postgres (requiere extensión pgcrypto para gen_random_uuid)
            e.Property(u => u.Id)
             .HasDefaultValueSql("gen_random_uuid()");

            // Propiedades básicas
            e.Property(u => u.firstName)
             .IsRequired()
             .HasMaxLength(100);

            e.Property(u => u.surName)
             .IsRequired()
             .HasMaxLength(100);

            e.Property(u => u.cedula)
             .IsRequired()
             .HasMaxLength(50);

            e.Property(u => u.telefono)
             .IsRequired()
             .HasMaxLength(50);

            e.Property(u => u.ubicacion)
             .IsRequired()
             .HasMaxLength(100);

            e.Property(u => u.IsActive)
             .HasDefaultValue(true);

            e.Property(u => u.IsLocked)
             .HasDefaultValue(false);

            e.Property(u => u.FailedAttempts)
             .HasDefaultValue(0);

            e.Property(u => u.CreatedAt)
             .HasDefaultValueSql("now()");

            // Sombra UpdatedAt (si la usas desde SaveChanges para setearla)
            e.Property<DateTime>("UpdatedAt")
             .IsRequired()
             .HasDefaultValueSql("now()")
             .ValueGeneratedOnAddOrUpdate();

            // ─────────────────────────────────────────────────────────────
            // Relaciones con eliminación en cascada
            // ─────────────────────────────────────────────────────────────

            e.HasMany(u => u.UserLogins)
             .WithOne(ul => ul.User)
             .HasForeignKey(ul => ul.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(u => u.UserCompanies)
             .WithOne(uc => uc.User)
             .HasForeignKey(uc => uc.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(u => u.UserRoles)
             .WithOne(ur => ur.User)
             .HasForeignKey(ur => ur.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            // (Opcional) Índices útiles — descomenta si aplica a tu negocio
            // e.HasIndex(u => u.cedula).HasDatabaseName("ix_users_cedula").IsUnique();
            // e.HasIndex(u => u.IsActive).HasDatabaseName("ix_users_is_active");
        }
    }
}
