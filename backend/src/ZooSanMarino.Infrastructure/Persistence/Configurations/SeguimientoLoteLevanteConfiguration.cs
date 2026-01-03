using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class SeguimientoLoteLevanteConfiguration : IEntityTypeConfiguration<SeguimientoLoteLevante>
{
    public void Configure(EntityTypeBuilder<SeguimientoLoteLevante> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();

        builder.Property(x => x.LoteId).IsRequired();
        builder.Property(x => x.TipoAlimento).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Observaciones).HasMaxLength(1000);

        // Consumos (en kg, convertidos)
        builder.Property(x => x.ConsumoKgHembras).HasPrecision(12, 3);
        builder.Property(x => x.ConsumoKgMachos).HasPrecision(12, 3);
        
        // Metadata JSONB para campos adicionales/extras
        builder.Property(x => x.Metadata)
               .HasColumnName("metadata")
               .HasColumnType("jsonb");

        // Pesos promedio (kg)
        builder.Property(x => x.PesoPromH).HasPrecision(8, 2);
        builder.Property(x => x.PesoPromM).HasPrecision(8, 2);

        // Uniformidades (%)
        builder.Property(x => x.UniformidadH).HasPrecision(5, 2);
        builder.Property(x => x.UniformidadM).HasPrecision(5, 2);

        // Coeficientes de variación
        builder.Property(x => x.CvH).HasPrecision(6, 3);
        builder.Property(x => x.CvM).HasPrecision(6, 3);

        // Métricas derivadas hembras
        builder.Property(x => x.KcalAlH).HasPrecision(12, 3);
        builder.Property(x => x.ProtAlH).HasPrecision(12, 3);
        builder.Property(x => x.KcalAveH).HasPrecision(12, 3);
        builder.Property(x => x.ProtAveH).HasPrecision(12, 3);

        builder.Property(x => x.Ciclo).HasMaxLength(50).HasDefaultValue("Normal");

        builder.HasOne(x => x.Lote)
               .WithMany()
               .HasForeignKey(x => x.LoteId)
               .OnDelete(DeleteBehavior.Restrict);

        // (Opcional) Índice único natural si lo usas en upsert (recomendado)
        // builder.HasIndex(x => new { x.LoteId, x.FechaRegistro }).IsUnique();
    }
}
