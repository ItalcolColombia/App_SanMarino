// src/ZooSanMarino.Infrastructure/Persistence/Configurations/Implementacion/ImplementacionTareaConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ImplementacionTareaConfiguration : IEntityTypeConfiguration<ImplementacionTarea>
{
    public void Configure(EntityTypeBuilder<ImplementacionTarea> b)
    {
        b.ToTable("implementacion_tareas", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        b.Property(x => x.Categoria).HasColumnName("categoria").HasMaxLength(100).IsRequired();
        b.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(300).IsRequired();
        b.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(2000);
        b.Property(x => x.Orden).HasColumnName("orden").HasDefaultValue(0).IsRequired();

        b.Property(x => x.FechaProgramada).HasColumnName("fecha_programada").HasColumnType("date");

        b.Property(x => x.RoleId).HasColumnName("role_id");
        b.Property(x => x.AsignadoUserId).HasColumnName("asignado_user_id");

        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20)
            .HasDefaultValue("pendiente").IsRequired();

        b.Property(x => x.FechaCompletada).HasColumnName("fecha_completada");
        b.Property(x => x.CompletadaPorUserId).HasColumnName("completada_por_user_id");
        b.Property(x => x.FechaConfirmada).HasColumnName("fecha_confirmada");
        b.Property(x => x.ConfirmadaPorUserId).HasColumnName("confirmada_por_user_id");

        b.Property(x => x.Observaciones).HasColumnName("observaciones").HasMaxLength(2000);

        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasIndex(x => x.PlanId).HasDatabaseName("ix_implementacion_tareas_plan");
        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_implementacion_tareas_company");
        b.HasIndex(x => x.AsignadoUserId).HasDatabaseName("ix_implementacion_tareas_asignado");

        b.ToTable(t => t.HasCheckConstraint(
            "ck_implementacion_tarea_estado",
            "estado IN ('pendiente', 'completada', 'confirmada')"));

        // La relación con el plan (cascade) se configura en ImplementacionPlanConfiguration.
        b.HasOne(x => x.Role).WithMany()
            .HasForeignKey(x => x.RoleId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AsignadoUser).WithMany()
            .HasForeignKey(x => x.AsignadoUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CompletadaPorUser).WithMany()
            .HasForeignKey(x => x.CompletadaPorUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ConfirmadaPorUser).WithMany()
            .HasForeignKey(x => x.ConfirmadaPorUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
