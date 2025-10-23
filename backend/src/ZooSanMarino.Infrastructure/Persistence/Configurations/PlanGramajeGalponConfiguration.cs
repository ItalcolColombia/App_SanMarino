// PlanGramajeGalponConfiguration.cs
// ⚠️ Asumiendo que PlanGramajeGalpon.GalponId ES string para alinear con Galpon
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

public class PlanGramajeGalponConfiguration : IEntityTypeConfiguration<PlanGramajeGalpon>
{
    public void Configure(EntityTypeBuilder<PlanGramajeGalpon> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();

        builder.Property(x => x.GalponId).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TipoAlimento).HasMaxLength(100);
        builder.Property(x => x.GramajeGrPorAve).HasPrecision(12,3);
        builder.Property(x => x.Observaciones).HasMaxLength(1000);
        builder.Property(x => x.Vigente).HasDefaultValue(true);

        builder.HasOne<Galpon>()
               .WithMany()
               .HasForeignKey(x => x.GalponId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
