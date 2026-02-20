// file: src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteSeguimientoConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;


namespace ZooSanMarino.Infrastructure.Persistence.Configurations;


public class LoteSeguimientoConfiguration : IEntityTypeConfiguration<LoteSeguimiento>
{
public void Configure(EntityTypeBuilder<LoteSeguimiento> b)
{
b.ToTable("lote_seguimientos", schema: "public");
b.HasKey(x => x.Id);


b.Property(x => x.Id).HasColumnName("id");
b.Property(x => x.Fecha).HasColumnName("fecha").IsRequired();
b.Property(x => x.LoteId).HasColumnName("lote_id").HasMaxLength(64).IsRequired();
b.Property(x => x.ReproductoraId).HasColumnName("reproductora_id").HasMaxLength(64).IsRequired();


b.Property(x => x.PesoInicial).HasColumnName("peso_inicial").HasPrecision(10,3);
b.Property(x => x.PesoFinal).HasColumnName("peso_final").HasPrecision(10,3);
b.Property(x => x.MortalidadM).HasColumnName("mortalidad_m");
b.Property(x => x.MortalidadH).HasColumnName("mortalidad_h");
b.Property(x => x.SelM).HasColumnName("sel_m");
b.Property(x => x.SelH).HasColumnName("sel_h");
b.Property(x => x.ErrorM).HasColumnName("error_m");
b.Property(x => x.ErrorH).HasColumnName("error_h");
b.Property(x => x.TipoAlimento).HasColumnName("tipo_alimento").HasMaxLength(100);
b.Property(x => x.ConsumoAlimento).HasColumnName("consumo_alimento").HasPrecision(10,3);
b.Property(x => x.ConsumoKgMachos).HasColumnName("consumo_kg_machos").HasPrecision(10,3);
b.Property(x => x.Observaciones).HasColumnName("observaciones").HasMaxLength(1000);
b.Property(x => x.Ciclo).HasColumnName("ciclo").HasMaxLength(50).HasDefaultValue("Normal");

// Campos de peso y uniformidad (double precision)
b.Property(x => x.PesoPromH).HasColumnName("peso_prom_h").HasColumnType("double precision");
b.Property(x => x.PesoPromM).HasColumnName("peso_prom_m").HasColumnType("double precision");
b.Property(x => x.UniformidadH).HasColumnName("uniformidad_h").HasColumnType("double precision");
b.Property(x => x.UniformidadM).HasColumnName("uniformidad_m").HasColumnType("double precision");
b.Property(x => x.CvH).HasColumnName("cv_h").HasColumnType("double precision");
b.Property(x => x.CvM).HasColumnName("cv_m").HasColumnType("double precision");

// Campos de agua (double precision)
b.Property(x => x.ConsumoAguaDiario).HasColumnName("consumo_agua_diario").HasColumnType("double precision");
b.Property(x => x.ConsumoAguaPh).HasColumnName("consumo_agua_ph").HasColumnType("double precision");
b.Property(x => x.ConsumoAguaOrp).HasColumnName("consumo_agua_orp").HasColumnType("double precision");
b.Property(x => x.ConsumoAguaTemperatura).HasColumnName("consumo_agua_temperatura").HasColumnType("double precision");

// Campos JSONB
b.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
b.Property(x => x.ItemsAdicionales).HasColumnName("items_adicionales").HasColumnType("jsonb");


b.HasOne(x => x.LoteReproductora)
.WithMany(x => x.LoteSeguimientos)
.HasForeignKey(x => new { x.LoteId, x.ReproductoraId })
.OnDelete(DeleteBehavior.Cascade);


// Relación con Lote: lote_id es string pero en lotes es integer
// La validación se maneja manualmente en el servicio
// b.HasOne(x => x.Lote)
//     .WithMany()
//     .HasForeignKey(x => x.LoteId)
//     .OnDelete(DeleteBehavior.Restrict);


b.ToTable(t =>
{
t.HasCheckConstraint("ck_ls_nonneg_counts", "(mortalidad_m >= 0 OR mortalidad_m IS NULL) AND (mortalidad_h >= 0 OR mortalidad_h IS NULL) AND (sel_m >= 0 OR sel_m IS NULL) AND (sel_h >= 0 OR sel_h IS NULL)");
t.HasCheckConstraint("ck_ls_nonneg_pesos", "(peso_inicial >= 0 OR peso_inicial IS NULL) AND (peso_final >= 0 OR peso_final IS NULL) AND (consumo_alimento >= 0 OR consumo_alimento IS NULL) AND (consumo_kg_machos >= 0 OR consumo_kg_machos IS NULL)");
t.HasCheckConstraint("ck_ls_uniformidad", "(uniformidad_h >= 0 AND uniformidad_h <= 100 OR uniformidad_h IS NULL) AND (uniformidad_m >= 0 AND uniformidad_m <= 100 OR uniformidad_m IS NULL)");
});


b.HasIndex(x => new { x.LoteId, x.ReproductoraId, x.Fecha }).HasDatabaseName("ix_ls_lote_rep_fecha");
}
}