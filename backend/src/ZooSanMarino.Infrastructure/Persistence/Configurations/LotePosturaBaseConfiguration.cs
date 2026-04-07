using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LotePosturaBaseConfiguration : IEntityTypeConfiguration<LotePosturaBase>
{
    public void Configure(EntityTypeBuilder<LotePosturaBase> b)
    {
        b.ToTable("lote_postura_base", schema: "public");
        b.HasKey(x => x.LotePosturaBaseId);

        b.Property(x => x.LotePosturaBaseId)
            .HasColumnName("lote_postura_base_id")
            .ValueGeneratedOnAdd();

        b.Property(x => x.CodigoErp)
            .HasColumnName("codigo_erp")
            .HasMaxLength(80);

        b.Property(x => x.LoteNombre)
            .HasColumnName("lote_nombre")
            .HasMaxLength(200)
            .IsRequired();

        b.Property(x => x.CantidadHembras).HasColumnName("cantidad_hembras").IsRequired();
        b.Property(x => x.CantidadMachos).HasColumnName("cantidad_machos").IsRequired();
        b.Property(x => x.CantidadMixtas).HasColumnName("cantidad_mixtas").IsRequired();

        b.Property(x => x.PaisId).HasColumnName("pais_id");

        // AuditableEntity
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_lote_postura_base_company");
        b.HasIndex(x => x.CodigoErp).HasDatabaseName("ix_lote_postura_base_codigo_erp");

        b.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_lpb_nonneg_counts",
                "cantidad_hembras >= 0 AND cantidad_machos >= 0 AND cantidad_mixtas >= 0"
            );
        });
    }
}

