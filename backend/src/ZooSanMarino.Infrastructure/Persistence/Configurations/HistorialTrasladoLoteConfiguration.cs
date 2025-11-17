// src/ZooSanMarino.Infrastructure/Persistence/Configurations/HistorialTrasladoLoteConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class HistorialTrasladoLoteConfiguration : IEntityTypeConfiguration<HistorialTrasladoLote>
{
    public void Configure(EntityTypeBuilder<HistorialTrasladoLote> b)
    {
        b.ToTable("historial_traslado_lote");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

        b.Property(x => x.LoteOriginalId).HasColumnName("lote_original_id").IsRequired();
        b.Property(x => x.LoteNuevoId).HasColumnName("lote_nuevo_id").IsRequired();
        b.Property(x => x.GranjaOrigenId).HasColumnName("granja_origen_id").IsRequired();
        b.Property(x => x.GranjaDestinoId).HasColumnName("granja_destino_id").IsRequired();
        b.Property(x => x.NucleoDestinoId).HasColumnName("nucleo_destino_id").HasMaxLength(50);
        b.Property(x => x.GalponDestinoId).HasColumnName("galpon_destino_id").HasMaxLength(50);
        b.Property(x => x.Observaciones).HasColumnName("observaciones").HasMaxLength(1000);
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        // Relaciones opcionales (sin foreign keys estrictas para evitar problemas de cascada)
        b.HasOne(x => x.LoteOriginal)
            .WithMany()
            .HasForeignKey(x => x.LoteOriginalId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.LoteNuevo)
            .WithMany()
            .HasForeignKey(x => x.LoteNuevoId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.GranjaOrigen)
            .WithMany()
            .HasForeignKey(x => x.GranjaOrigenId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.GranjaDestino)
            .WithMany()
            .HasForeignKey(x => x.GranjaDestinoId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.LoteOriginalId);
        b.HasIndex(x => x.LoteNuevoId);
        b.HasIndex(x => x.CompanyId);
        b.HasIndex(x => x.CreatedAt);
    }
}

