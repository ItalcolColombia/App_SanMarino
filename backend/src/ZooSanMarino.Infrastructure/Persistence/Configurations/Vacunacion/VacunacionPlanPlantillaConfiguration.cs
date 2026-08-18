// src/ZooSanMarino.Infrastructure/Persistence/Configurations/Vacunacion/VacunacionPlanPlantillaConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class VacunacionPlanPlantillaConfiguration : IEntityTypeConfiguration<VacunacionPlanPlantilla>
{
    public void Configure(EntityTypeBuilder<VacunacionPlanPlantilla> b)
    {
        b.ToTable("vacunacion_plan_plantilla", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.PaisId).HasColumnName("pais_id");

        b.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
        b.Property(x => x.LineaProductiva).HasColumnName("linea_productiva").HasMaxLength(20).IsRequired();
        b.Property(x => x.Raza).HasColumnName("raza").HasMaxLength(100);
        b.Property(x => x.VigenteDesde).HasColumnName("vigente_desde").HasColumnType("date");
        b.Property(x => x.Activa).HasColumnName("activa").HasDefaultValue(true).IsRequired();
        b.Property(x => x.Notas).HasColumnName("notas").HasMaxLength(2000);

        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        // La consulta caliente: las plantillas candidatas de un lote (empresa + línea).
        b.HasIndex(x => new { x.CompanyId, x.LineaProductiva })
            .HasDatabaseName("ix_vacunacion_plan_plantilla_company_linea");

        b.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_vpp_linea_valida",
                "linea_productiva IN ('Levante', 'Produccion', 'Engorde')");
        });
    }
}
