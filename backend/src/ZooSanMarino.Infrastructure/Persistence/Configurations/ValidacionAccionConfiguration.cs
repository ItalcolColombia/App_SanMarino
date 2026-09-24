using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ValidacionAccionConfiguration : IEntityTypeConfiguration<ValidacionAccion>
{
    public void Configure(EntityTypeBuilder<ValidacionAccion> e)
    {
        e.ToTable("validacion_acciones", "public", t =>
        {
            t.HasCheckConstraint("ck_validacion_acciones_accion_valida",
                "accion IN ('APROBAR','DEVOLVER','EDITAR','REENVIAR','ELIMINAR','CANCELAR','OVERRIDE')");
        });

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.InstanciaId).HasColumnName("instancia_id").IsRequired();
        e.Property(x => x.InstanciaPasoId).HasColumnName("instancia_paso_id").IsRequired();
        e.Property(x => x.Accion).HasColumnName("accion").HasMaxLength(12).IsRequired();
        e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        e.Property(x => x.AsignadoId).HasColumnName("asignado_id");
        e.Property(x => x.Comentario).HasColumnName("comentario");
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz")
            .HasDefaultValueSql("now()").IsRequired();

        e.HasIndex(x => x.InstanciaId).HasDatabaseName("ix_validacion_acciones_instancia_id");
        e.HasIndex(x => new { x.InstanciaPasoId, x.Accion }).HasDatabaseName("ix_validacion_acciones_paso_accion");
        // "misma persona no firma dos veces la misma etapa con APROBAR" se garantiza por SQL crudo
        // (índice único parcial filtrado por accion='APROBAR') en la migración, ver §obligatorio.

        e.HasOne(x => x.Instancia).WithMany(i => i.Acciones).HasForeignKey(x => x.InstanciaId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.InstanciaPaso).WithMany().HasForeignKey(x => x.InstanciaPasoId).OnDelete(DeleteBehavior.Restrict);
    }
}
