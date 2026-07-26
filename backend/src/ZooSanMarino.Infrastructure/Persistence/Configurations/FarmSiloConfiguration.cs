// src/ZooSanMarino.Infrastructure/Persistence/Configurations/FarmSiloConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class FarmSiloConfiguration : IEntityTypeConfiguration<FarmSilo>
{
    public void Configure(EntityTypeBuilder<FarmSilo> b)
    {
        b.ToTable("farm_silos", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.GranjaId).HasColumnName("granja_id").IsRequired();

        b.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(120)
            .IsRequired();

        // 'Silo' (alimento) | 'Insumos' (bodega)
        b.Property(x => x.Tipo)
            .HasColumnName("tipo")
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.CodigoErpUbicacion)
            .HasColumnName("codigo_erp_ubicacion")
            .HasMaxLength(20)
            .IsRequired(false);

        b.Property(x => x.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(200)
            .IsRequired(false);

        b.Property(x => x.CentroOperacion)
            .HasColumnName("centro_operacion")
            .HasMaxLength(20)
            .IsRequired(false);

        b.Property(x => x.CodigoBodega)
            .HasColumnName("codigo_bodega")
            .HasMaxLength(20)
            .IsRequired(false);

        b.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        b.Property(x => x.CreatedAt).HasColumnName("created_at");

        // FK a la granja (sin cascada: el catálogo se limpia explícitamente)
        b.HasOne<Farm>()
            .WithMany()
            .HasForeignKey(x => x.GranjaId)
            .HasConstraintName("fk_farm_silos_farm")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.GranjaId).HasDatabaseName("ix_farm_silos_granja");
        b.HasIndex(x => new { x.GranjaId, x.Nombre })
            .IsUnique()
            .HasDatabaseName("ux_farm_silos_granja_nombre");
    }
}
