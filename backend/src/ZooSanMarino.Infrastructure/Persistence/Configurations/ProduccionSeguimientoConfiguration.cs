// src/ZooSanMarino.Infrastructure/Persistence/Configurations/ProduccionSeguimientoConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ProduccionSeguimientoConfiguration : IEntityTypeConfiguration<ProduccionSeguimiento>
{
    public void Configure(EntityTypeBuilder<ProduccionSeguimiento> builder)
    {
        builder.ToTable("produccion_seguimiento");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.LoteId)
            .IsRequired()
            .HasColumnName("lote_id");

        builder.Property(x => x.FechaRegistro)
            .IsRequired()
            .HasColumnName("fecha_registro")
            .HasColumnType("date");

        builder.Property(x => x.MortalidadH)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("mortalidad_h");

        builder.Property(x => x.MortalidadM)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("mortalidad_m");

        builder.Property(x => x.ConsumoKg)
            .IsRequired()
            .HasDefaultValue(0)
            .HasPrecision(10, 2)
            .HasColumnName("consumo_kg");

        builder.Property(x => x.HuevosTotales)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevos_totales");

        builder.Property(x => x.HuevosIncubables)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevos_incubables");

        builder.Property(x => x.PesoHuevo)
            .IsRequired()
            .HasDefaultValue(0)
            .HasPrecision(8, 2)
            .HasColumnName("peso_huevo");

        builder.Property(x => x.Observaciones)
            .HasMaxLength(1000)
            .HasColumnName("observaciones");

        // Traslado de aves (R3)
        builder.Property(x => x.TrasladoHembras).HasColumnName("traslado_hembras");
        builder.Property(x => x.TrasladoMachos).HasColumnName("traslado_machos");
        builder.Property(x => x.LoteDestinoId).HasColumnName("lote_destino_id");
        builder.Property(x => x.GranjaDestinoId).HasColumnName("granja_destino_id");
        builder.Property(x => x.FechaTraslado).HasColumnName("fecha_traslado").HasColumnType("date");
        builder.Property(x => x.TrasladoObservaciones).HasMaxLength(500).HasColumnName("traslado_observaciones");

        // Feature 14 — splits H/M dedicados
        builder.Property(x => x.TrasladoIngresoHembras).HasColumnName("traslado_ingreso_hembras").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.TrasladoIngresoMachos ).HasColumnName("traslado_ingreso_machos" ).HasDefaultValue(0).IsRequired();
        builder.Property(x => x.TrasladoSalidaHembras ).HasColumnName("traslado_salida_hembras" ).HasDefaultValue(0).IsRequired();
        builder.Property(x => x.TrasladoSalidaMachos  ).HasColumnName("traslado_salida_machos"  ).HasDefaultValue(0).IsRequired();

        // Feature 14 — marcado de traslado (igual que Levante)
        builder.Property(x => x.EsTraslado).HasColumnName("es_traslado").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.TrasladoLoteContraparteId).HasColumnName("traslado_lote_contraparte_id");
        builder.Property(x => x.TrasladoGranjaContraparteId).HasColumnName("traslado_granja_contraparte_id");
        builder.Property(x => x.TrasladoDireccion).HasColumnName("traslado_direccion").HasMaxLength(10);
        builder.HasIndex(x => x.EsTraslado).HasDatabaseName("idx_produccion_seguimiento_es_traslado");

        // Feature 14 — selección y error de sexaje
        builder.Property(x => x.SelH).HasColumnName("sel_h").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.SelM).HasColumnName("sel_m").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.ErrorSexajeHembras).HasColumnName("error_sexaje_hembras").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.ErrorSexajeMachos ).HasColumnName("error_sexaje_machos" ).HasDefaultValue(0).IsRequired();

        // Feature 14 — auditoría (UpdatedByUserId es int? heredado de AuditableEntity)
        builder.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");

        // Relación con Lote (lote en fase Producción)
        builder.HasOne(x => x.Lote)
            .WithMany(x => x.ProduccionSeguimientos)
            .HasForeignKey(x => x.LoteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índices
        builder.HasIndex(x => new { x.LoteId, x.FechaRegistro })
            .IsUnique()
            .HasDatabaseName("IX_produccion_seguimiento_lote_fecha_unique");

        builder.HasIndex(x => x.FechaRegistro)
            .HasDatabaseName("IX_produccion_seguimiento_fecha_registro");
    }
}



