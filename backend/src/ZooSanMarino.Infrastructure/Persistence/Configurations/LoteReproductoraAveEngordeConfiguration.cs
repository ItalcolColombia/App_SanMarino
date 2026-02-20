// src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteReproductoraAveEngordeConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LoteReproductoraAveEngordeConfiguration : IEntityTypeConfiguration<LoteReproductoraAveEngorde>
{
    public void Configure(EntityTypeBuilder<LoteReproductoraAveEngorde> b)
    {
        b.ToTable("lote_reproductora_ave_engorde");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.LoteAveEngordeId).HasColumnName("lote_ave_engorde_id").IsRequired();
        b.Property(x => x.ReproductoraId).HasColumnName("reproductora_id").HasMaxLength(64).IsRequired();
        b.Property(x => x.NombreLote).HasColumnName("nombre_lote").HasMaxLength(200).IsRequired();
        b.Property(x => x.FechaEncasetamiento).HasColumnName("fecha_encasetamiento");

        b.Property(x => x.M).HasColumnName("m");
        b.Property(x => x.H).HasColumnName("h");
        b.Property(x => x.AvesInicioHembras).HasColumnName("aves_inicio_hembras");
        b.Property(x => x.AvesInicioMachos).HasColumnName("aves_inicio_machos");
        b.Property(x => x.Mixtas).HasColumnName("mixtas");
        b.Property(x => x.MortCajaH).HasColumnName("mort_caja_h");
        b.Property(x => x.MortCajaM).HasColumnName("mort_caja_m");
        b.Property(x => x.UnifH).HasColumnName("unif_h");
        b.Property(x => x.UnifM).HasColumnName("unif_m");

        b.Property(x => x.PesoInicialM).HasColumnName("peso_inicial_m").HasPrecision(10, 3);
        b.Property(x => x.PesoInicialH).HasColumnName("peso_inicial_h").HasPrecision(10, 3);
        b.Property(x => x.PesoMixto).HasColumnName("peso_mixto").HasPrecision(10, 3);

        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasOne(x => x.LoteAveEngorde)
            .WithMany()
            .HasForeignKey(x => x.LoteAveEngordeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.LoteAveEngordeId).HasDatabaseName("ix_lote_reproductora_ave_engorde_lote");
        b.HasIndex(x => x.ReproductoraId).HasDatabaseName("ix_lote_reproductora_ave_engorde_reproductora");
        b.HasIndex(x => x.FechaEncasetamiento).HasDatabaseName("ix_lote_reproductora_ave_engorde_fecha");
    }
}
