// src/ZooSanMarino.Infrastructure/Persistence/Configurations/Implementacion/ImplementacionPlanConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ImplementacionPlanConfiguration : IEntityTypeConfiguration<ImplementacionPlan>
{
    public void Configure(EntityTypeBuilder<ImplementacionPlan> b)
    {
        b.ToTable("implementacion_planes", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.PaisId).HasColumnName("pais_id");

        b.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
        b.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(2000);

        b.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20)
            .HasDefaultValue("implementacion").IsRequired();

        b.Property(x => x.FechaInicio).HasColumnName("fecha_inicio").HasColumnType("date");
        b.Property(x => x.FechaFin).HasColumnName("fecha_fin").HasColumnType("date");

        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20)
            .HasDefaultValue("borrador").IsRequired();

        b.Property(x => x.ImplementadorUserId).HasColumnName("implementador_user_id");
        b.Property(x => x.CreadoPorUserGuid).HasColumnName("creado_por_user_guid");

        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasIndex(x => new { x.CompanyId, x.PaisId }).HasDatabaseName("ix_implementacion_planes_company_pais");
        b.HasIndex(x => x.ImplementadorUserId).HasDatabaseName("ix_implementacion_planes_implementador");

        b.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_implementacion_plan_estado",
                "estado IN ('borrador', 'en_progreso', 'completado', 'cancelado')");
            t.HasCheckConstraint(
                "ck_implementacion_plan_tipo",
                "tipo IN ('implementacion', 'capacitacion', 'mixto')");
        });

        b.HasMany(x => x.Tareas)
            .WithOne(t => t.Plan)
            .HasForeignKey(t => t.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ImplementadorUser).WithMany()
            .HasForeignKey(x => x.ImplementadorUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreadoPorUser).WithMany()
            .HasForeignKey(x => x.CreadoPorUserGuid)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
