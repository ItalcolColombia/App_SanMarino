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
b.Property(x => x.LoteId).HasColumnName("lote_id").IsRequired();
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
b.Property(x => x.Observaciones).HasColumnName("observaciones").HasMaxLength(1000);


b.HasOne(x => x.LoteReproductora)
.WithMany(x => x.LoteSeguimientos)
.HasForeignKey(x => new { x.LoteId, x.ReproductoraId })
.OnDelete(DeleteBehavior.Cascade);


b.HasOne(x => x.Lote)
.WithMany()
.HasForeignKey(x => x.LoteId)
.OnDelete(DeleteBehavior.Restrict);


b.ToTable(t =>
{
t.HasCheckConstraint("ck_ls_nonneg_counts", "(mortalidad_m >= 0 OR mortalidad_m IS NULL) AND (mortalidad_h >= 0 OR mortalidad_h IS NULL) AND (sel_m >= 0 OR sel_m IS NULL) AND (sel_h >= 0 OR sel_h IS NULL)");
t.HasCheckConstraint("ck_ls_nonneg_pesos", "(peso_inicial >= 0 OR peso_inicial IS NULL) AND (peso_final >= 0 OR peso_final IS NULL) AND (consumo_alimento >= 0 OR consumo_alimento IS NULL)");
});


b.HasIndex(x => new { x.LoteId, x.ReproductoraId, x.Fecha }).HasDatabaseName("ix_ls_lote_rep_fecha");
}
}