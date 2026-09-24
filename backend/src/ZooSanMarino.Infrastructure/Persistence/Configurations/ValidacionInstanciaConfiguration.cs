using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ValidacionInstanciaConfiguration : IEntityTypeConfiguration<ValidacionInstancia>
{
    public void Configure(EntityTypeBuilder<ValidacionInstancia> e)
    {
        e.ToTable("validacion_instancias", "public", t =>
        {
            t.HasCheckConstraint("ck_validacion_instancias_estado_valido",
                "estado IN ('PENDIENTE_VALIDACION','DEVUELTA_CORRECCION','APROBADA','CANCELADA','ERROR_FINALIZACION')");
            t.HasCheckConstraint("ck_validacion_instancias_intento_positivo", "intento > 0");
        });

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        e.Property(x => x.ProcesoId).HasColumnName("proceso_id").IsRequired();
        e.Property(x => x.FlujoId).HasColumnName("flujo_id").IsRequired();
        e.Property(x => x.RecursoTipo).HasColumnName("recurso_tipo").HasMaxLength(60).IsRequired();
        e.Property(x => x.RecursoId).HasColumnName("recurso_id").HasMaxLength(64).IsRequired();
        e.Property(x => x.Intento).HasColumnName("intento").HasDefaultValue(1).IsRequired();
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(24)
            .HasDefaultValue(EstadoValidacionInstancia.PendienteValidacion).IsRequired();
        e.Property(x => x.PasoActualOrden).HasColumnName("paso_actual_orden").IsRequired();
        e.Property(x => x.PasoRetornoOrden).HasColumnName("paso_retorno_orden");
        e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();

        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz")
            .HasDefaultValueSql("now()").IsRequired();
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
        e.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamptz");
        e.Property(x => x.ReturnedAt).HasColumnName("returned_at").HasColumnType("timestamptz");
        e.Property(x => x.CancelledAt).HasColumnName("cancelled_at").HasColumnType("timestamptz");
        e.Property(x => x.MotivoEstado).HasColumnName("motivo_estado");

        e.HasIndex(x => new { x.CompanyId, x.ProcesoId, x.RecursoTipo, x.RecursoId })
            .HasDatabaseName("ix_validacion_instancias_recurso");
        e.HasIndex(x => new { x.CompanyId, x.Estado }).HasDatabaseName("ix_validacion_instancias_company_estado");
        // Índice único parcial "una instancia activa por recurso" va por SQL crudo en la migración
        // (filtro por estado ACTIVOS): ver AddFlujosValidacionParametrizables.

        e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Proceso).WithMany().HasForeignKey(x => x.ProcesoId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Flujo).WithMany(f => f.Instancias).HasForeignKey(x => x.FlujoId).OnDelete(DeleteBehavior.Restrict);

        e.HasMany(x => x.Pasos).WithOne(p => p.Instancia).HasForeignKey(p => p.InstanciaId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Acciones).WithOne(a => a.Instancia).HasForeignKey(a => a.InstanciaId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Novedades).WithOne(n => n.Instancia).HasForeignKey(n => n.InstanciaId).OnDelete(DeleteBehavior.Cascade);
    }
}
