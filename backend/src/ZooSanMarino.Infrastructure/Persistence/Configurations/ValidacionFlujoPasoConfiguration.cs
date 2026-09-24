using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ValidacionFlujoPasoConfiguration : IEntityTypeConfiguration<ValidacionFlujoPaso>
{
    public void Configure(EntityTypeBuilder<ValidacionFlujoPaso> e)
    {
        e.ToTable("validacion_flujo_pasos", "public", t =>
        {
            t.HasCheckConstraint("ck_validacion_flujo_pasos_orden_rango", "orden BETWEEN 1 AND 20");
            t.HasCheckConstraint("ck_validacion_flujo_pasos_aprobaciones_positivas", "aprobaciones_requeridas > 0");
        });

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.FlujoId).HasColumnName("flujo_id").IsRequired();
        e.Property(x => x.Orden).HasColumnName("orden").IsRequired();
        e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(120).IsRequired();
        e.Property(x => x.AprobacionesRequeridas).HasColumnName("aprobaciones_requeridas").HasDefaultValue(1).IsRequired();
        e.Property(x => x.Descripcion).HasColumnName("descripcion");

        e.HasIndex(x => new { x.FlujoId, x.Orden }).IsUnique().HasDatabaseName("ux_validacion_flujo_pasos_flujo_orden");

        e.HasOne(x => x.Flujo).WithMany(f => f.Pasos).HasForeignKey(x => x.FlujoId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Asignados).WithOne(a => a.Paso).HasForeignKey(a => a.PasoId).OnDelete(DeleteBehavior.Cascade);
    }
}
