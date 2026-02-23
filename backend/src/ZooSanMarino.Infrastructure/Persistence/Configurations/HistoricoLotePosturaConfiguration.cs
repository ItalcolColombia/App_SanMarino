// src/ZooSanMarino.Infrastructure/Persistence/Configurations/HistoricoLotePosturaConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class HistoricoLotePosturaConfiguration : IEntityTypeConfiguration<HistoricoLotePostura>
{
    public void Configure(EntityTypeBuilder<HistoricoLotePostura> b)
    {
        b.ToTable("historico_lote_postura", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.TipoLote).HasColumnName("tipo_lote").HasMaxLength(32).IsRequired();
        b.Property(x => x.LotePosturaLevanteId).HasColumnName("lote_postura_levante_id");
        b.Property(x => x.LotePosturaProduccionId).HasColumnName("lote_postura_produccion_id");
        b.Property(x => x.TipoRegistro).HasColumnName("tipo_registro").HasMaxLength(24).IsRequired().HasDefaultValue("Creacion");
        b.Property(x => x.FechaRegistro).HasColumnName("fecha_registro").IsRequired();
        b.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        b.Property(x => x.Snapshot).HasColumnName("snapshot").HasColumnType("jsonb");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_historico_lote_postura_company");
        b.HasIndex(x => x.TipoLote).HasDatabaseName("ix_historico_lote_postura_tipo");
        b.HasIndex(x => x.LotePosturaLevanteId).HasDatabaseName("ix_historico_lote_postura_levante").HasFilter("lote_postura_levante_id IS NOT NULL");
        b.HasIndex(x => x.LotePosturaProduccionId).HasDatabaseName("ix_historico_lote_postura_produccion").HasFilter("lote_postura_produccion_id IS NOT NULL");
        b.HasIndex(x => x.FechaRegistro).HasDatabaseName("ix_historico_lote_postura_fecha");

        b.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_hlp_tipo_lote",
                "tipo_lote IN ('LotePosturaLevante', 'LotePosturaProduccion')"
            );
            t.HasCheckConstraint(
                "ck_hlp_tipo_registro",
                "tipo_registro IN ('Creacion', 'Actualizacion')"
            );
            t.HasCheckConstraint(
                "ck_hlp_lote_ref",
                "(tipo_lote = 'LotePosturaLevante' AND lote_postura_levante_id IS NOT NULL AND lote_postura_produccion_id IS NULL) OR (tipo_lote = 'LotePosturaProduccion' AND lote_postura_levante_id IS NULL AND lote_postura_produccion_id IS NOT NULL)"
            );
        });

        b.HasOne(x => x.LotePosturaLevante)
            .WithMany()
            .HasForeignKey(x => x.LotePosturaLevanteId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.LotePosturaProduccion)
            .WithMany()
            .HasForeignKey(x => x.LotePosturaProduccionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
