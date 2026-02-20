using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class HistorialLotePolloEngordeConfiguration : IEntityTypeConfiguration<HistorialLotePolloEngorde>
{
    public void Configure(EntityTypeBuilder<HistorialLotePolloEngorde> b)
    {
        b.ToTable("historial_lote_pollo_engorde");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.TipoLote).HasColumnName("tipo_lote").HasMaxLength(32).IsRequired();
        b.Property(x => x.LoteAveEngordeId).HasColumnName("lote_ave_engorde_id");
        b.Property(x => x.LoteReproductoraAveEngordeId).HasColumnName("lote_reproductora_ave_engorde_id");
        b.Property(x => x.TipoRegistro).HasColumnName("tipo_registro").HasMaxLength(24).HasDefaultValue("Inicio");
        b.Property(x => x.AvesHembras).HasColumnName("aves_hembras").HasDefaultValue(0);
        b.Property(x => x.AvesMachos).HasColumnName("aves_machos").HasDefaultValue(0);
        b.Property(x => x.AvesMixtas).HasColumnName("aves_mixtas").HasDefaultValue(0);
        b.Property(x => x.FechaRegistro).HasColumnName("fecha_registro").HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.MovimientoId).HasColumnName("movimiento_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        b.Ignore(x => x.TotalAves);

        b.HasOne(x => x.LoteAveEngorde)
            .WithMany()
            .HasForeignKey(x => x.LoteAveEngordeId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.LoteReproductoraAveEngorde)
            .WithMany()
            .HasForeignKey(x => x.LoteReproductoraAveEngordeId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Movimiento)
            .WithMany()
            .HasForeignKey(x => x.MovimientoId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_hlpe_company_id");
        b.HasIndex(x => x.TipoLote).HasDatabaseName("ix_hlpe_tipo_lote");
        b.HasIndex(x => x.LoteAveEngordeId).HasDatabaseName("ix_hlpe_lote_ave_engorde_id");
        b.HasIndex(x => x.LoteReproductoraAveEngordeId).HasDatabaseName("ix_hlpe_lote_reproductora_id");
        b.HasIndex(x => x.FechaRegistro).HasDatabaseName("ix_hlpe_fecha_registro");
    }
}
