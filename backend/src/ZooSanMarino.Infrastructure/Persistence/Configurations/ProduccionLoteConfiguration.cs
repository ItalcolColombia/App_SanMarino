// src/ZooSanMarino.Infrastructure/Persistence/Configurations/ProduccionLoteConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ProduccionLoteConfiguration : IEntityTypeConfiguration<ProduccionLote>
{
    public void Configure(EntityTypeBuilder<ProduccionLote> builder)
    {
        builder.ToTable("produccion_lotes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.LoteId)
            .IsRequired()
            .HasColumnType("character varying");

        builder.Property(x => x.FechaInicio)
            .IsRequired()
            .HasColumnName("fecha_inicio_produccion");

        builder.Property(x => x.AvesInicialesH)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("hembras_iniciales");

        builder.Property(x => x.AvesInicialesM)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("machos_iniciales");

        builder.Property(x => x.HuevosIniciales)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevos_iniciales");

        builder.Property(x => x.TipoNido)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("Manual")
            .HasColumnName("tipo_nido");

        builder.Property(x => x.GranjaId)
            .IsRequired()
            .HasColumnName("granja_id");

        builder.Property(x => x.NucleoId)
            .IsRequired()
            .HasColumnName("nucleo_id")
            .HasColumnType("character varying");

        builder.Property(x => x.NucleoP)
            .HasMaxLength(100)
            .HasColumnName("nucleo_p")
            .HasColumnType("character varying");

        builder.Property(x => x.GalponId)
            .HasColumnName("galpon_id")
            .HasColumnType("character varying");

        builder.Property(x => x.Ciclo)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("normal")
            .HasColumnName("ciclo");

        // Relación con Lote - Comentado porque lote_id es VARCHAR
        // builder.HasOne(x => x.Lote)
        //     .WithMany()
        //     .HasForeignKey(x => x.LoteId)
        //     .OnDelete(DeleteBehavior.Restrict);

        // Índice único para asegurar un solo registro inicial por lote
        builder.HasIndex(x => x.LoteId)
            .IsUnique()
            .HasDatabaseName("IX_produccion_lote_lote_id_unique");

        // Índices adicionales
        builder.HasIndex(x => x.FechaInicio)
            .HasDatabaseName("IX_produccion_lote_fecha_inicio");
    }
}