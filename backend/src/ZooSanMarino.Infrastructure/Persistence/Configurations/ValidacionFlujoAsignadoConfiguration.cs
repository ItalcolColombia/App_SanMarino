using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ValidacionFlujoAsignadoConfiguration : IEntityTypeConfiguration<ValidacionFlujoAsignado>
{
    public void Configure(EntityTypeBuilder<ValidacionFlujoAsignado> e)
    {
        e.ToTable("validacion_flujo_asignados", "public", t =>
        {
            t.HasCheckConstraint("ck_validacion_flujo_asignados_tipo_valido", "tipo IN ('ROL','USUARIO')");
            t.HasCheckConstraint("ck_validacion_flujo_asignados_tipo_responsable",
                "(tipo = 'ROL' AND role_id IS NOT NULL AND user_id IS NULL) OR " +
                "(tipo = 'USUARIO' AND user_id IS NOT NULL AND role_id IS NULL)");
        });

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.PasoId).HasColumnName("paso_id").IsRequired();
        e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(10).IsRequired();
        e.Property(x => x.RoleId).HasColumnName("role_id");
        e.Property(x => x.UserId).HasColumnName("user_id");
        e.Property(x => x.RolFiltroOrigenId).HasColumnName("rol_filtro_origen_id");

        e.HasIndex(x => x.PasoId).HasDatabaseName("ix_validacion_flujo_asignados_paso_id");
        e.HasIndex(x => new { x.PasoId, x.RoleId }).HasDatabaseName("ix_validacion_flujo_asignados_paso_role");
        e.HasIndex(x => new { x.PasoId, x.UserId }).HasDatabaseName("ix_validacion_flujo_asignados_paso_user");

        e.HasOne(x => x.Paso).WithMany(p => p.Asignados).HasForeignKey(x => x.PasoId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        // UserId NO lleva FK fluida a User (Guid) a propósito: se valida en el service que el
        // usuario exista y esté activo en la empresa del flujo, igual que el resto del repo.
    }
}
