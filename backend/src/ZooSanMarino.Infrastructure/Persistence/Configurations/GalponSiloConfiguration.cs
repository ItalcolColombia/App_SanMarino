// src/ZooSanMarino.Infrastructure/Persistence/Configurations/GalponSiloConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class GalponSiloConfiguration : IEntityTypeConfiguration<GalponSilo>
{
    public void Configure(EntityTypeBuilder<GalponSilo> b)
    {
        b.ToTable("galpon_silos", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.GranjaId).HasColumnName("granja_id").IsRequired();

        b.Property(x => x.NucleoId)
            .HasColumnName("nucleo_id")
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.GalponId)
            .HasColumnName("galpon_id")
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.FarmSiloId).HasColumnName("farm_silo_id").IsRequired();

        b.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired(false);

        // Restrict: un silo con galpones asignados no se borra físicamente (baja lógica en farm_silos).
        b.HasOne(x => x.FarmSilo)
            .WithMany()
            .HasForeignKey(x => x.FarmSiloId)
            .HasConstraintName("fk_galpon_silos_farm_silo")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.GranjaId, x.NucleoId, x.GalponId, x.FarmSiloId })
            .IsUnique()
            .HasDatabaseName("ux_galpon_silos_galpon_silo");

        b.HasIndex(x => x.FarmSiloId).HasDatabaseName("ix_galpon_silos_silo");
        b.HasIndex(x => new { x.GranjaId, x.GalponId }).HasDatabaseName("ix_galpon_silos_granja_galpon");
    }
}
