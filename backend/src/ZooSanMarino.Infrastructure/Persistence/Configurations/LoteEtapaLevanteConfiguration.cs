// src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteEtapaLevanteConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LoteEtapaLevanteConfiguration : IEntityTypeConfiguration<LoteEtapaLevante>
{
    public void Configure(EntityTypeBuilder<LoteEtapaLevante> b)
    {
        b.ToTable("lote_etapa_levante", "public");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.LoteId).HasColumnName("lote_id").IsRequired();
        b.Property(x => x.AvesInicioHembras).HasColumnName("aves_inicio_hembras").IsRequired();
        b.Property(x => x.AvesInicioMachos).HasColumnName("aves_inicio_machos").IsRequired();
        b.Property(x => x.FechaInicio).HasColumnName("fecha_inicio").IsRequired();
        b.Property(x => x.FechaFin).HasColumnName("fecha_fin");
        b.Property(x => x.AvesFinHembras).HasColumnName("aves_fin_hembras");
        b.Property(x => x.AvesFinMachos).HasColumnName("aves_fin_machos");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(x => x.LoteId).IsUnique().HasDatabaseName("uq_lote_etapa_levante_lote");
        b.HasOne(x => x.Lote)
            .WithMany()
            .HasForeignKey(x => x.LoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
