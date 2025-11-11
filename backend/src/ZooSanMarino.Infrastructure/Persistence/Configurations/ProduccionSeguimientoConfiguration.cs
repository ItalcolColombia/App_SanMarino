// src/ZooSanMarino.Infrastructure/Persistence/Configurations/ProduccionSeguimientoConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ProduccionSeguimientoConfiguration : IEntityTypeConfiguration<ProduccionSeguimiento>
{
    public void Configure(EntityTypeBuilder<ProduccionSeguimiento> builder)
    {
        builder.ToTable("produccion_seguimiento");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.ProduccionLoteId)
            .IsRequired()
            .HasColumnName("produccion_lote_id");

        builder.Property(x => x.FechaRegistro)
            .IsRequired()
            .HasColumnName("fecha_registro")
            .HasColumnType("date");

        builder.Property(x => x.MortalidadH)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("mortalidad_h");

        builder.Property(x => x.MortalidadM)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("mortalidad_m");

        builder.Property(x => x.ConsumoKg)
            .IsRequired()
            .HasDefaultValue(0)
            .HasPrecision(10, 2)
            .HasColumnName("consumo_kg");

        builder.Property(x => x.HuevosTotales)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevos_totales");

        builder.Property(x => x.HuevosIncubables)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevos_incubables");

        builder.Property(x => x.PesoHuevo)
            .IsRequired()
            .HasDefaultValue(0)
            .HasPrecision(8, 2)
            .HasColumnName("peso_huevo");

        builder.Property(x => x.Observaciones)
            .HasMaxLength(1000)
            .HasColumnName("observaciones");

        // Relación con ProduccionLote
        builder.HasOne(x => x.ProduccionLote)
            .WithMany(x => x.Seguimientos)
            .HasForeignKey(x => x.ProduccionLoteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índices
        builder.HasIndex(x => new { x.ProduccionLoteId, x.FechaRegistro })
            .IsUnique()
            .HasDatabaseName("IX_produccion_seguimiento_lote_fecha_unique");

        builder.HasIndex(x => x.FechaRegistro)
            .HasDatabaseName("IX_produccion_seguimiento_fecha_registro");
    }
}



