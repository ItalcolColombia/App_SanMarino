using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class VisitaTecnicaConfiguration : IEntityTypeConfiguration<VisitaTecnica>
{
    public void Configure(EntityTypeBuilder<VisitaTecnica> b)
    {
        b.ToTable("visitas_tecnicas", "public", t => t.HasCheckConstraint(
            "ck_visitas_tecnicas_estado", "estado IN ('PROGRAMADA', 'REALIZADA', 'CANCELADA')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.FarmId).HasColumnName("farm_id").IsRequired();
        b.Property(x => x.NucleoId).HasColumnName("nucleo_id").HasMaxLength(80);
        b.Property(x => x.GalponId).HasColumnName("galpon_id").HasMaxLength(80);
        b.Property(x => x.LoteId).HasColumnName("lote_id");
        b.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        b.Property(x => x.Objetivo).HasColumnName("objetivo").HasMaxLength(2000);
        b.Property(x => x.FechaProgramada).HasColumnName("fecha_programada").IsRequired();
        b.Property(x => x.FechaRealizada).HasColumnName("fecha_realizada");
        b.Property(x => x.Observaciones).HasColumnName("observaciones").HasMaxLength(4000);
        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(16).HasDefaultValue(EstadoVisitaTecnica.Programada).IsRequired();
        b.Property(x => x.VeterinarioUserId).HasColumnName("veterinario_user_id").IsRequired();
        MapAudit(b);
        b.HasIndex(x => new { x.CompanyId, x.FechaProgramada }).HasDatabaseName("ix_visitas_tecnicas_company_fecha");
        b.HasIndex(x => new { x.VeterinarioUserId, x.Estado }).HasDatabaseName("ix_visitas_tecnicas_veterinario_estado");
        b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.VeterinarioUser).WithMany().HasForeignKey(x => x.VeterinarioUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void MapAudit(EntityTypeBuilder<VisitaTecnica> b)
    {
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
    }
}
