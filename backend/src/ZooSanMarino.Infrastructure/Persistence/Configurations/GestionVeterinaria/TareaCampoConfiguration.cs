using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class TareaCampoConfiguration : IEntityTypeConfiguration<TareaCampo>
{
    public void Configure(EntityTypeBuilder<TareaCampo> b)
    {
        b.ToTable("tareas_campo", "public", t => t.HasCheckConstraint(
            "ck_tareas_campo_estado", "estado IN ('PENDIENTE', 'REALIZADA', 'CANCELADA')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.VisitaId).HasColumnName("visita_id");
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.FarmId).HasColumnName("farm_id").IsRequired();
        b.Property(x => x.NucleoId).HasColumnName("nucleo_id").HasMaxLength(80);
        b.Property(x => x.GalponId).HasColumnName("galpon_id").HasMaxLength(80);
        b.Property(x => x.LoteId).HasColumnName("lote_id");
        b.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        b.Property(x => x.Instrucciones).HasColumnName("instrucciones").HasMaxLength(3000);
        b.Property(x => x.FechaInicio).HasColumnName("fecha_inicio").HasColumnType("date").IsRequired();
        b.Property(x => x.FechaFin).HasColumnName("fecha_fin").HasColumnType("date").IsRequired();
        b.Property(x => x.RequiereObservacion).HasColumnName("requiere_observacion").HasDefaultValue(false).IsRequired();
        b.Property(x => x.RequiereFoto).HasColumnName("requiere_foto").HasDefaultValue(false).IsRequired();
        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(16).HasDefaultValue(EstadoTareaCampo.Pendiente).IsRequired();
        b.Property(x => x.CreadaPorUserId).HasColumnName("creada_por_user_id").IsRequired();
        b.Property(x => x.RealizadaPorUserId).HasColumnName("realizada_por_user_id");
        b.Property(x => x.FechaRealizada).HasColumnName("fecha_realizada");
        b.Property(x => x.ObservacionCumplimiento).HasColumnName("observacion_cumplimiento").HasMaxLength(2000);
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        b.HasIndex(x => new { x.CompanyId, x.Estado, x.FechaFin }).HasDatabaseName("ix_tareas_campo_company_estado_fecha");
        b.HasIndex(x => new { x.FarmId, x.NucleoId, x.GalponId, x.LoteId }).HasDatabaseName("ix_tareas_campo_ubicacion");
        b.HasIndex(x => x.CreadaPorUserId).HasDatabaseName("ix_tareas_campo_creada_por");
        b.HasOne(x => x.Visita).WithMany(x => x.Tareas).HasForeignKey(x => x.VisitaId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreadaPorUser).WithMany().HasForeignKey(x => x.CreadaPorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RealizadaPorUser).WithMany().HasForeignKey(x => x.RealizadaPorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
