// file: src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteReproductoraConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;


namespace ZooSanMarino.Infrastructure.Persistence.Configurations;


public class LoteReproductoraConfiguration : IEntityTypeConfiguration<LoteReproductora>
{
    public void Configure(EntityTypeBuilder<LoteReproductora> b)
    {
    b.ToTable("lote_reproductoras", schema: "public");
    b.HasKey(x => new { x.LoteId, x.ReproductoraId });


    b.Property(x => x.LoteId)
        .HasColumnName("lote_id")
        .HasMaxLength(64)
        .IsRequired();
    b.Property(x => x.ReproductoraId).HasColumnName("reproductora_id").HasMaxLength(64).IsRequired();
    b.Property(x => x.NombreLote).HasColumnName("nombre_lote").HasMaxLength(200).IsRequired();
    b.Property(x => x.FechaEncasetamiento).HasColumnName("fecha_encasetamiento");


    b.Property(x => x.M).HasColumnName("m");
    b.Property(x => x.H).HasColumnName("h");
    b.Property(x => x.AvesInicioHembras).HasColumnName("aves_inicio_hembras");
    b.Property(x => x.AvesInicioMachos).HasColumnName("aves_inicio_machos");
    b.Property(x => x.Mixtas).HasColumnName("mixtas");
    b.Property(x => x.MortCajaH).HasColumnName("mort_caja_h");
    b.Property(x => x.MortCajaM).HasColumnName("mort_caja_m");
    b.Property(x => x.UnifH).HasColumnName("unif_h");
    b.Property(x => x.UnifM).HasColumnName("unif_m");


    b.Property(x => x.PesoInicialM).HasColumnName("peso_inicial_m").HasPrecision(10,3);
    b.Property(x => x.PesoInicialH).HasColumnName("peso_inicial_h").HasPrecision(10,3);
    b.Property(x => x.PesoMixto).HasColumnName("peso_mixto").HasPrecision(10,3);


    // Relación con Lote: lote_id en lote_reproductoras es string (character varying),
    // pero en lotes es integer. No podemos usar foreign key automática por el desajuste de tipos.
    // La validación de integridad referencial se maneja manualmente en el servicio.
    // Ignoramos la propiedad de navegación Lote para evitar que EF Core intente crear la relación:
    // b.Ignore(x => x.Lote); // Comentado porque la propiedad ya está comentada en la entidad


    b.HasMany(x => x.LoteGalpones)
    .WithOne(x => x.LoteReproductora)
    .HasForeignKey(x => new { x.LoteId, x.ReproductoraId })
    .OnDelete(DeleteBehavior.Cascade);


    b.HasMany(x => x.LoteSeguimientos)
    .WithOne(x => x.LoteReproductora)
    .HasForeignKey(x => new { x.LoteId, x.ReproductoraId })
    .OnDelete(DeleteBehavior.Cascade);


    // Índices y checks útiles
    b.HasIndex(x => x.LoteId).HasDatabaseName("ix_lote_reproductora_lote");
    b.HasIndex(x => x.ReproductoraId).HasDatabaseName("ix_lote_reproductora_rep");
    b.HasIndex(x => x.FechaEncasetamiento).HasDatabaseName("ix_lote_reproductora_fecha");


    b.ToTable(t =>
    {
    t.HasCheckConstraint("ck_lr_nonneg_counts", "(m >= 0 OR m IS NULL) AND (h >= 0 OR h IS NULL) AND (mixtas >= 0 OR mixtas IS NULL)");
    t.HasCheckConstraint("ck_lr_nonneg_pesos", "(peso_inicial_m >= 0 OR peso_inicial_m IS NULL) AND (peso_inicial_h >= 0 OR peso_inicial_h IS NULL) AND (peso_mixto >= 0 OR peso_mixto IS NULL)");
    });
    }
}