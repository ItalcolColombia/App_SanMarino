using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ValidacionNovedadUsuarioConfiguration : IEntityTypeConfiguration<ValidacionNovedadUsuario>
{
    public void Configure(EntityTypeBuilder<ValidacionNovedadUsuario> e)
    {
        e.ToTable("validacion_novedades_usuario", "public", t =>
        {
            t.HasCheckConstraint("ck_validacion_novedades_estado_valido",
                "estado IN ('ACTIVA','RESUELTA','CANCELADA')");
            t.HasCheckConstraint("ck_validacion_novedades_resolucion_valida",
                "resolucion IS NULL OR resolucion IN ('CORREGIDO_REENVIADO','DEVUELTO_ATRAS','ELIMINADO')");
        });

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.InstanciaId).HasColumnName("instancia_id").IsRequired();
        e.Property(x => x.AccionDevolucionId).HasColumnName("accion_devolucion_id").IsRequired();
        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        e.Property(x => x.DestinatarioUserId).HasColumnName("destinatario_user_id").IsRequired();
        e.Property(x => x.GeneradaPorUserId).HasColumnName("generada_por_user_id").IsRequired();
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(12)
            .HasDefaultValue(EstadoNovedadValidacion.Activa).IsRequired();
        e.Property(x => x.Motivo).HasColumnName("motivo").IsRequired();
        e.Property(x => x.ContextoResumen).HasColumnName("contexto_resumen").HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb").IsRequired();

        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz")
            .HasDefaultValueSql("now()").IsRequired();
        e.Property(x => x.ReadAt).HasColumnName("read_at").HasColumnType("timestamptz");
        e.Property(x => x.ResolvedAt).HasColumnName("resolved_at").HasColumnType("timestamptz");
        e.Property(x => x.ResolvedByUserId).HasColumnName("resolved_by_user_id");
        e.Property(x => x.Resolucion).HasColumnName("resolucion").HasMaxLength(24);

        e.HasIndex(x => new { x.CompanyId, x.DestinatarioUserId, x.Estado })
            .HasDatabaseName("ix_validacion_novedades_destinatario_estado");
        e.HasIndex(x => x.InstanciaId).HasDatabaseName("ix_validacion_novedades_instancia_id");

        e.HasOne(x => x.Instancia).WithMany(i => i.Novedades).HasForeignKey(x => x.InstanciaId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.AccionDevolucion).WithMany().HasForeignKey(x => x.AccionDevolucionId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
