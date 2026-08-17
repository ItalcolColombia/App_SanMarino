// src/ZooSanMarino.Infrastructure/Persistence/Configurations/Vacunacion/VacunacionPlanPlantillaItemConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class VacunacionPlanPlantillaItemConfiguration : IEntityTypeConfiguration<VacunacionPlanPlantillaItem>
{
    public void Configure(EntityTypeBuilder<VacunacionPlanPlantillaItem> b)
    {
        b.ToTable("vacunacion_plan_plantilla_item", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.PlantillaId).HasColumnName("plantilla_id").IsRequired();
        b.Property(x => x.ItemInventarioId).HasColumnName("item_inventario_id").IsRequired();

        b.Property(x => x.UnidadObjetivo).HasColumnName("unidad_objetivo").HasMaxLength(10).IsRequired();
        b.Property(x => x.ValorObjetivo).HasColumnName("valor_objetivo").IsRequired();
        b.Property(x => x.RangoDiasAntes).HasColumnName("rango_dias_antes").HasDefaultValue(0).IsRequired();
        b.Property(x => x.RangoDiasDespues).HasColumnName("rango_dias_despues").HasDefaultValue(0).IsRequired();

        b.Property(x => x.Orden).HasColumnName("orden").HasDefaultValue(0).IsRequired();
        b.Property(x => x.Notas).HasColumnName("notas").HasMaxLength(2000);

        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasIndex(x => x.PlantillaId).HasDatabaseName("ix_vacunacion_plan_plantilla_item_plantilla");

        b.ToTable(t =>
        {
            // 'Fecha' NO entra a propósito: una fecha fija no se puede plantillar, sería la misma
            // para lotes encasetados en meses distintos. Lo mismo valida MotivoItemInvalido, que
            // además puede explicarlo; el CHECK es el respaldo por si alguien escribe por SQL.
            t.HasCheckConstraint("ck_vppi_unidad_valida", "unidad_objetivo IN ('Semana', 'Dia')");
            t.HasCheckConstraint("ck_vppi_valor_nonneg", "valor_objetivo >= 0");
            t.HasCheckConstraint("ck_vppi_rango_nonneg", "rango_dias_antes >= 0 AND rango_dias_despues >= 0");
        });

        b.HasOne(x => x.Plantilla).WithMany(p => p.Items)
            .HasForeignKey(x => x.PlantillaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ItemInventario).WithMany()
            .HasForeignKey(x => x.ItemInventarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
