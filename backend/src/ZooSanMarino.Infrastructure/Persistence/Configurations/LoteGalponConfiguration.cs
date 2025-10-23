// file: src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteGalponConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;


namespace ZooSanMarino.Infrastructure.Persistence.Configurations;


public class LoteGalponConfiguration : IEntityTypeConfiguration<LoteGalpon>
    {
    public void Configure(EntityTypeBuilder<LoteGalpon> b)
    {
    b.ToTable("lote_galpones", schema: "public");
    b.HasKey(x => new { x.LoteId, x.ReproductoraId, x.GalponId });


    b.Property(x => x.LoteId).HasColumnName("lote_id").IsRequired();
    b.Property(x => x.ReproductoraId).HasColumnName("reproductora_id").HasMaxLength(64).IsRequired();
    b.Property(x => x.GalponId).HasColumnName("galpon_id").HasMaxLength(64).IsRequired();
    b.Property(x => x.M).HasColumnName("m");
    b.Property(x => x.H).HasColumnName("h");


    b.HasOne(x => x.LoteReproductora)
    .WithMany(x => x.LoteGalpones)
    .HasForeignKey(x => new { x.LoteId, x.ReproductoraId })
    .OnDelete(DeleteBehavior.Cascade);


    b.HasOne(x => x.Galpon)
    .WithMany()
    .HasForeignKey(x => x.GalponId)
    .OnDelete(DeleteBehavior.Restrict);


    b.ToTable(t =>
    {
    t.HasCheckConstraint("ck_lg_nonneg_counts", "(m >= 0 OR m IS NULL) AND (h >= 0 OR h IS NULL)");
    });
    }
}