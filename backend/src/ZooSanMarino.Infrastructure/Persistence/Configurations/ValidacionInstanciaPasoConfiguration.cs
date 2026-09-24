using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ValidacionInstanciaPasoConfiguration : IEntityTypeConfiguration<ValidacionInstanciaPaso>
{
    public void Configure(EntityTypeBuilder<ValidacionInstanciaPaso> e)
    {
        e.ToTable("validacion_instancia_pasos", "public", t =>
        {
            t.HasCheckConstraint("ck_validacion_instancia_pasos_estado_valido",
                "estado IN ('BLOQUEADA','PENDIENTE','APROBADA','DEVUELTA','ESPERANDO_CORRECCION','CANCELADA')");
        });

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.InstanciaId).HasColumnName("instancia_id").IsRequired();
        e.Property(x => x.PasoDefinicionId).HasColumnName("paso_definicion_id").IsRequired();
        e.Property(x => x.Orden).HasColumnName("orden").IsRequired();
        e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(120).IsRequired();
        e.Property(x => x.AprobacionesRequeridas).HasColumnName("aprobaciones_requeridas").HasDefaultValue(1).IsRequired();
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(24)
            .HasDefaultValue(EstadoInstanciaPaso.Bloqueada).IsRequired();
        e.Property(x => x.OpenedAt).HasColumnName("opened_at").HasColumnType("timestamptz");
        e.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamptz");

        e.HasIndex(x => new { x.InstanciaId, x.Orden }).IsUnique().HasDatabaseName("ux_validacion_instancia_pasos_instancia_orden");
        e.HasIndex(x => new { x.InstanciaId, x.Estado }).HasDatabaseName("ix_validacion_instancia_pasos_instancia_estado");

        e.HasOne(x => x.Instancia).WithMany(i => i.Pasos).HasForeignKey(x => x.InstanciaId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.PasoDefinicion).WithMany().HasForeignKey(x => x.PasoDefinicionId).OnDelete(DeleteBehavior.Restrict);
    }
}
