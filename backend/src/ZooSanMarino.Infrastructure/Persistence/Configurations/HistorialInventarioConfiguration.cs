// src/ZooSanMarino.Infrastructure/Persistence/Configurations/HistorialInventarioConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class HistorialInventarioConfiguration : IEntityTypeConfiguration<HistorialInventario>
{
    public void Configure(EntityTypeBuilder<HistorialInventario> builder)
    {
        // Tabla
        builder.ToTable("historial_inventario");

        // Clave primaria
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        // Referencias
        builder.Property(x => x.InventarioId)
            .HasColumnName("inventario_id")
            .IsRequired();

        builder.Property(x => x.LoteId)
            .HasColumnName("lote_id")
            .IsRequired();

        // Información del cambio
        builder.Property(x => x.FechaCambio)
            .HasColumnName("fecha_cambio")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.TipoCambio)
            .HasColumnName("tipo_cambio")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.MovimientoId)
            .HasColumnName("movimiento_id");

        // Cantidades anteriores
        builder.Property(x => x.CantidadHembrasAnterior)
            .HasColumnName("cantidad_hembras_anterior")
            .HasDefaultValue(0);

        builder.Property(x => x.CantidadMachosAnterior)
            .HasColumnName("cantidad_machos_anterior")
            .HasDefaultValue(0);

        builder.Property(x => x.CantidadMixtasAnterior)
            .HasColumnName("cantidad_mixtas_anterior")
            .HasDefaultValue(0);

        // Cantidades nuevas
        builder.Property(x => x.CantidadHembrasNueva)
            .HasColumnName("cantidad_hembras_nueva")
            .HasDefaultValue(0);

        builder.Property(x => x.CantidadMachosNueva)
            .HasColumnName("cantidad_machos_nueva")
            .HasDefaultValue(0);

        builder.Property(x => x.CantidadMixtasNueva)
            .HasColumnName("cantidad_mixtas_nueva")
            .HasDefaultValue(0);

        // Ubicación
        builder.Property(x => x.GranjaId)
            .HasColumnName("granja_id")
            .IsRequired();

        builder.Property(x => x.NucleoId)
            .HasColumnName("nucleo_id")
            .HasMaxLength(50);

        builder.Property(x => x.GalponId)
            .HasColumnName("galpon_id")
            .HasMaxLength(50);

        // Usuario
        builder.Property(x => x.UsuarioCambioId)
            .HasColumnName("usuario_cambio_id")
            .IsRequired();

        builder.Property(x => x.UsuarioNombre)
            .HasColumnName("usuario_nombre")
            .HasMaxLength(200);

        // Información adicional
        builder.Property(x => x.Motivo)
            .HasColumnName("motivo")
            .HasMaxLength(500);

        builder.Property(x => x.Observaciones)
            .HasColumnName("observaciones")
            .HasMaxLength(1000);

        // Propiedades de auditoría
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
        builder.HasOne(x => x.Inventario)
            .WithMany()
            .HasForeignKey(x => x.InventarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_historial_inventario_inventario_id");

        builder.HasOne(x => x.Lote)
            .WithMany()
            .HasForeignKey(x => x.LoteId)
            .HasPrincipalKey(l => l.LoteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_historial_inventario_lote_id");

        builder.HasOne(x => x.Movimiento)
            .WithMany()
            .HasForeignKey(x => x.MovimientoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_historial_inventario_movimiento_id");

        builder.HasOne(x => x.Granja)
            .WithMany()
            .HasForeignKey(x => x.GranjaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_historial_inventario_granja_id");

        builder.HasOne(x => x.Nucleo)
            .WithMany()
            .HasForeignKey(x => new { x.NucleoId, x.GranjaId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_historial_inventario_nucleo_id_granja_id");

        builder.HasOne(x => x.Galpon)
            .WithMany()
            .HasForeignKey(x => x.GalponId)
            .HasPrincipalKey(g => g.GalponId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_historial_inventario_galpon_id");

        // Índices
        builder.HasIndex(x => x.InventarioId)
            .HasDatabaseName("ix_historial_inventario_inventario_id");

        builder.HasIndex(x => x.LoteId)
            .HasDatabaseName("ix_historial_inventario_lote_id");

        builder.HasIndex(x => x.FechaCambio)
            .HasDatabaseName("ix_historial_inventario_fecha_cambio");

        builder.HasIndex(x => x.TipoCambio)
            .HasDatabaseName("ix_historial_inventario_tipo_cambio");

        builder.HasIndex(x => x.MovimientoId)
            .HasDatabaseName("ix_historial_inventario_movimiento_id");

        builder.HasIndex(x => x.UsuarioCambioId)
            .HasDatabaseName("ix_historial_inventario_usuario_cambio_id");

        builder.HasIndex(x => x.CompanyId)
            .HasDatabaseName("ix_historial_inventario_company_id");

        builder.HasIndex(x => new { x.GranjaId, x.NucleoId, x.GalponId })
            .HasDatabaseName("ix_historial_inventario_ubicacion");
    }
}
