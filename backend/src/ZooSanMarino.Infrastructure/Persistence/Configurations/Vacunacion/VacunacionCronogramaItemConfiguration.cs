// src/ZooSanMarino.Infrastructure/Persistence/Configurations/Vacunacion/VacunacionCronogramaItemConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class VacunacionCronogramaItemConfiguration : IEntityTypeConfiguration<VacunacionCronogramaItem>
{
    public void Configure(EntityTypeBuilder<VacunacionCronogramaItem> b)
    {
        b.ToTable("vacunacion_cronograma_item", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.PaisId).HasColumnName("pais_id");

        b.Property(x => x.LineaProductiva).HasColumnName("linea_productiva").HasMaxLength(20).IsRequired();
        b.Property(x => x.LotePosturaLevanteId).HasColumnName("lote_postura_levante_id");
        b.Property(x => x.LotePosturaProduccionId).HasColumnName("lote_postura_produccion_id");
        b.Property(x => x.LoteAveEngordeId).HasColumnName("lote_ave_engorde_id");

        b.Property(x => x.GranjaId).HasColumnName("granja_id").IsRequired();
        b.Property(x => x.NucleoId).HasColumnName("nucleo_id").HasMaxLength(64);
        b.Property(x => x.GalponId).HasColumnName("galpon_id").HasMaxLength(64);

        b.Property(x => x.ItemInventarioId).HasColumnName("item_inventario_id").IsRequired();

        b.Property(x => x.UnidadObjetivo).HasColumnName("unidad_objetivo").HasMaxLength(10).IsRequired();
        b.Property(x => x.ValorObjetivo).HasColumnName("valor_objetivo");
        b.Property(x => x.FechaObjetivo).HasColumnName("fecha_objetivo").HasColumnType("date");

        b.Property(x => x.RangoDiasAntes).HasColumnName("rango_dias_antes").HasDefaultValue(0).IsRequired();
        b.Property(x => x.RangoDiasDespues).HasColumnName("rango_dias_despues").HasDefaultValue(0).IsRequired();

        b.Property(x => x.Orden).HasColumnName("orden").HasDefaultValue(0).IsRequired();
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true).IsRequired();
        b.Property(x => x.Notas).HasColumnName("notas").HasMaxLength(2000);

        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasIndex(x => x.GranjaId).HasDatabaseName("ix_vacunacion_cronograma_item_granja");
        b.HasIndex(x => x.LotePosturaLevanteId).HasDatabaseName("ix_vacunacion_cronograma_item_levante");
        b.HasIndex(x => x.LotePosturaProduccionId).HasDatabaseName("ix_vacunacion_cronograma_item_produccion");
        b.HasIndex(x => x.LoteAveEngordeId).HasDatabaseName("ix_vacunacion_cronograma_item_engorde");
        b.HasIndex(x => x.ItemInventarioId).HasDatabaseName("ix_vacunacion_cronograma_item_item_inventario");

        b.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_vci_linea_valida",
                "linea_productiva IN ('Levante', 'Produccion', 'Engorde')"
            );
            t.HasCheckConstraint(
                "ck_vci_unidad_valida",
                "unidad_objetivo IN ('Semana', 'Dia', 'Fecha')"
            );
            // Exactamente un FK de línea poblado, coherente con linea_productiva.
            t.HasCheckConstraint(
                "ck_vci_un_solo_lote",
                "(CASE WHEN lote_postura_levante_id IS NOT NULL THEN 1 ELSE 0 END" +
                " + CASE WHEN lote_postura_produccion_id IS NOT NULL THEN 1 ELSE 0 END" +
                " + CASE WHEN lote_ave_engorde_id IS NOT NULL THEN 1 ELSE 0 END) = 1"
            );
            // valor_objetivo obligatorio si Semana/Dia; fecha_objetivo obligatoria si Fecha.
            t.HasCheckConstraint(
                "ck_vci_objetivo_coherente",
                "(unidad_objetivo IN ('Semana','Dia') AND valor_objetivo IS NOT NULL AND fecha_objetivo IS NULL)" +
                " OR (unidad_objetivo = 'Fecha' AND fecha_objetivo IS NOT NULL AND valor_objetivo IS NULL)"
            );
            t.HasCheckConstraint("ck_vci_rango_nonneg", "rango_dias_antes >= 0 AND rango_dias_despues >= 0");
        });

        b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.GranjaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Nucleo).WithMany()
            .HasForeignKey(x => new { x.NucleoId, x.GranjaId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Galpon).WithMany()
            .HasForeignKey(x => x.GalponId)
            .HasPrincipalKey(g => g.GalponId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ItemInventario).WithMany()
            .HasForeignKey(x => x.ItemInventarioId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LotePosturaLevante).WithMany()
            .HasForeignKey(x => x.LotePosturaLevanteId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LotePosturaProduccion).WithMany()
            .HasForeignKey(x => x.LotePosturaProduccionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LoteAveEngorde).WithMany()
            .HasForeignKey(x => x.LoteAveEngordeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // La relación 1:1 con VacunacionRegistroAplicacion (FK del lado dependiente) se configura
        // en VacunacionRegistroAplicacionConfiguration para no duplicar la definición.
    }
}
