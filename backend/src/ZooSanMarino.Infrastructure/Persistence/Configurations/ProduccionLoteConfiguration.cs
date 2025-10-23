// src/ZooSanMarino.Infrastructure/Persistence/Configurations/ProduccionLoteConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ProduccionLoteConfiguration : IEntityTypeConfiguration<ProduccionLote>
{
    public void Configure(EntityTypeBuilder<ProduccionLote> builder)
    {
        builder.ToTable("produccion_lote");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.LoteId)
            .IsRequired();

        builder.Property(x => x.FechaInicio)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(x => x.AvesInicialesH)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.AvesInicialesM)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.Observaciones)
            .HasMaxLength(1000);

        // Relación con Lote
        builder.HasOne(x => x.Lote)
            .WithMany()
            .HasForeignKey(x => x.LoteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice único para asegurar un solo registro inicial por lote
        builder.HasIndex(x => x.LoteId)
            .IsUnique()
            .HasDatabaseName("IX_produccion_lote_lote_id_unique");

        // Índices adicionales
        builder.HasIndex(x => x.FechaInicio)
            .HasDatabaseName("IX_produccion_lote_fecha_inicio");
    }
}