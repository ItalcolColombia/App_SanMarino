// src/ZooSanMarino.Infrastructure/Persistence/Configurations/InventarioAvesConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class InventarioAvesConfiguration : IEntityTypeConfiguration<InventarioAves>
{
    public void Configure(EntityTypeBuilder<InventarioAves> builder)
    {
        // Tabla
        builder.ToTable("inventario_aves");

        // Clave primaria
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        // Propiedades básicas
        builder.Property(x => x.LoteId)
            .HasColumnName("lote_id")
            .IsRequired();

        builder.Property(x => x.GranjaId)
            .HasColumnName("granja_id")
            .IsRequired();

        builder.Property(x => x.NucleoId)
            .HasColumnName("nucleo_id")
            .HasMaxLength(50);

        builder.Property(x => x.GalponId)
            .HasColumnName("galpon_id")
            .HasMaxLength(50);

        // Cantidades
        builder.Property(x => x.CantidadHembras)
            .HasColumnName("cantidad_hembras")
            .HasDefaultValue(0);

        builder.Property(x => x.CantidadMachos)
            .HasColumnName("cantidad_machos")
            .HasDefaultValue(0);

        builder.Property(x => x.CantidadMixtas)
            .HasColumnName("cantidad_mixtas")
            .HasDefaultValue(0);

        // Fechas y estado
        builder.Property(x => x.FechaActualizacion)
            .HasColumnName("fecha_actualizacion")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.Estado)
            .HasColumnName("estado")
            .HasMaxLength(20)
            .HasDefaultValue("Activo");

        builder.Property(x => x.Observaciones)
            .HasColumnName("observaciones")
            .HasMaxLength(1000);

        // Propiedades de auditoría (heredadas de AuditableEntity)
        builder.Property(x => x.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedByUserId)
            .HasColumnName("updated_by_user_id");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");

        // Relaciones
        builder.HasOne(x => x.Lote)
            .WithMany()
            .HasForeignKey(x => x.LoteId)
            .HasPrincipalKey(l => l.LoteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_inventario_aves_lotes_lote_id");

        builder.HasOne(x => x.Granja)
            .WithMany()
            .HasForeignKey(x => x.GranjaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_inventario_aves_farms_granja_id");

        // Relación con Nucleo - clave compuesta
        builder.HasOne(x => x.Nucleo)
            .WithMany()
            .HasForeignKey(x => new { x.NucleoId, x.GranjaId })
            .HasPrincipalKey(n => new { n.NucleoId, n.GranjaId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_inventario_aves_nucleos_nucleo_id_granja_id");

        builder.HasOne(x => x.Galpon)
            .WithMany()
            .HasForeignKey(x => x.GalponId)
            .HasPrincipalKey(g => g.GalponId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_inventario_aves_galpones_galpon_id");

        // Índices
        builder.HasIndex(x => x.LoteId)
            .HasDatabaseName("ix_inventario_aves_lote_id");

        builder.HasIndex(x => new { x.GranjaId, x.NucleoId, x.GalponId })
            .HasDatabaseName("ix_inventario_aves_ubicacion");

        builder.HasIndex(x => x.CompanyId)
            .HasDatabaseName("ix_inventario_aves_company_id");

        builder.HasIndex(x => x.Estado)
            .HasDatabaseName("ix_inventario_aves_estado");

        builder.HasIndex(x => x.FechaActualizacion)
            .HasDatabaseName("ix_inventario_aves_fecha_actualizacion");

        // Índice único para evitar duplicados por ubicación
        builder.HasIndex(x => new { x.LoteId, x.GranjaId, x.NucleoId, x.GalponId, x.CompanyId })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("uq_inventario_aves_lote_ubicacion_company");
    }
}
