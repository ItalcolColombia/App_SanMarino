// src/ZooSanMarino.Infrastructure/Persistence/Configurations/MovimientoAvesConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class MovimientoAvesConfiguration : IEntityTypeConfiguration<MovimientoAves>
{
    public void Configure(EntityTypeBuilder<MovimientoAves> builder)
    {
        // Tabla
        builder.ToTable("movimiento_aves");

        // Clave primaria
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        // Información del movimiento
        builder.Property(x => x.NumeroMovimiento)
            .HasColumnName("numero_movimiento")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.FechaMovimiento)
            .HasColumnName("fecha_movimiento")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.TipoMovimiento)
            .HasColumnName("tipo_movimiento")
            .HasMaxLength(50)
            .IsRequired();

        // Origen
        builder.Property(x => x.InventarioOrigenId)
            .HasColumnName("inventario_origen_id");

        builder.Property(x => x.LoteOrigenId)
            .HasColumnName("lote_origen_id");

        builder.Property(x => x.GranjaOrigenId)
            .HasColumnName("granja_origen_id");

        builder.Property(x => x.NucleoOrigenId)
            .HasColumnName("nucleo_origen_id")
            .HasMaxLength(50);

        builder.Property(x => x.GalponOrigenId)
            .HasColumnName("galpon_origen_id")
            .HasMaxLength(50);

        // Destino
        builder.Property(x => x.InventarioDestinoId)
            .HasColumnName("inventario_destino_id");

        builder.Property(x => x.LoteDestinoId)
            .HasColumnName("lote_destino_id");

        builder.Property(x => x.GranjaDestinoId)
            .HasColumnName("granja_destino_id");

        builder.Property(x => x.NucleoDestinoId)
            .HasColumnName("nucleo_destino_id")
            .HasMaxLength(50);

        builder.Property(x => x.GalponDestinoId)
            .HasColumnName("galpon_destino_id")
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

        // Información adicional
        builder.Property(x => x.MotivoMovimiento)
            .HasColumnName("motivo_movimiento")
            .HasMaxLength(500);

        builder.Property(x => x.Observaciones)
            .HasColumnName("observaciones")
            .HasMaxLength(1000);

        builder.Property(x => x.Estado)
            .HasColumnName("estado")
            .HasMaxLength(20)
            .HasDefaultValue("Pendiente");

        // Usuario
        builder.Property(x => x.UsuarioMovimientoId)
            .HasColumnName("usuario_movimiento_id")
            .IsRequired();

        builder.Property(x => x.UsuarioNombre)
            .HasColumnName("usuario_nombre")
            .HasMaxLength(200);

        // Fechas de procesamiento
        builder.Property(x => x.FechaProcesamiento)
            .HasColumnName("fecha_procesamiento")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.FechaCancelacion)
            .HasColumnName("fecha_cancelacion")
            .HasColumnType("timestamp with time zone");

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
        builder.HasOne(x => x.InventarioOrigen)
            .WithMany(i => i.MovimientosOrigen)
            .HasForeignKey(x => x.InventarioOrigenId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_movimiento_aves_inventario_origen_id");

        builder.HasOne(x => x.InventarioDestino)
            .WithMany(i => i.MovimientosDestino)
            .HasForeignKey(x => x.InventarioDestinoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_movimiento_aves_inventario_destino_id");

        builder.HasOne(x => x.LoteOrigen)
            .WithMany()
            .HasForeignKey(x => x.LoteOrigenId)
            .HasPrincipalKey(l => l.LoteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_movimiento_aves_lote_origen_id");

        builder.HasOne(x => x.LoteDestino)
            .WithMany()
            .HasForeignKey(x => x.LoteDestinoId)
            .HasPrincipalKey(l => l.LoteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_movimiento_aves_lote_destino_id");

        builder.HasOne(x => x.GranjaOrigen)
            .WithMany()
            .HasForeignKey(x => x.GranjaOrigenId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_movimiento_aves_granja_origen_id");

        builder.HasOne(x => x.GranjaDestino)
            .WithMany()
            .HasForeignKey(x => x.GranjaDestinoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_movimiento_aves_granja_destino_id");

        // Índices
        builder.HasIndex(x => x.NumeroMovimiento)
            .IsUnique()
            .HasDatabaseName("uq_movimiento_aves_numero_movimiento");

        builder.HasIndex(x => x.FechaMovimiento)
            .HasDatabaseName("ix_movimiento_aves_fecha_movimiento");

        builder.HasIndex(x => x.TipoMovimiento)
            .HasDatabaseName("ix_movimiento_aves_tipo_movimiento");

        builder.HasIndex(x => x.Estado)
            .HasDatabaseName("ix_movimiento_aves_estado");

        builder.HasIndex(x => x.LoteOrigenId)
            .HasDatabaseName("ix_movimiento_aves_lote_origen_id");

        builder.HasIndex(x => x.LoteDestinoId)
            .HasDatabaseName("ix_movimiento_aves_lote_destino_id");

        builder.HasIndex(x => x.UsuarioMovimientoId)
            .HasDatabaseName("ix_movimiento_aves_usuario_movimiento_id");

        builder.HasIndex(x => x.CompanyId)
            .HasDatabaseName("ix_movimiento_aves_company_id");

        builder.HasIndex(x => new { x.GranjaOrigenId, x.GranjaDestinoId })
            .HasDatabaseName("ix_movimiento_aves_granjas");
    }
}
