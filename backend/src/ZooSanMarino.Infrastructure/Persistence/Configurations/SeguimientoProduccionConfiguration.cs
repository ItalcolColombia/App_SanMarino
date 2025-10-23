using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class SeguimientoProduccionConfiguration : IEntityTypeConfiguration<SeguimientoProduccion>
{
    public void Configure(EntityTypeBuilder<SeguimientoProduccion> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();

        builder.Property(x => x.LoteId).IsRequired();
        builder.Property(x => x.Fecha).IsRequired();
        builder.Property(x => x.TipoAlimento).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Observaciones).HasMaxLength(1000);

        // Consumos (decimal con precisión)
        builder.Property(x => x.ConsKgH).HasPrecision(12, 3);
        builder.Property(x => x.ConsKgM).HasPrecision(12, 3);

        // Peso del huevo (decimal con precisión)
        builder.Property(x => x.PesoHuevo).HasPrecision(8, 2);

        // Etapa (1, 2, 3)
        builder.Property(x => x.Etapa).IsRequired();

        // Relación con Lote
        builder.HasOne(x => x.Lote)
               .WithMany()
               .HasForeignKey(x => x.LoteId)
               .OnDelete(DeleteBehavior.Restrict);

        // Índice único por lote y fecha
        builder.HasIndex(x => new { x.LoteId, x.Fecha }).IsUnique();
    }
}



