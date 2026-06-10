using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LiquidacionLoteEngordePanamaConfiguration : IEntityTypeConfiguration<LiquidacionLoteEngordePanama>
{
    public void Configure(EntityTypeBuilder<LiquidacionLoteEngordePanama> b)
    {
        b.ToTable("liquidacion_lote_engorde_panama", schema: "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.LoteAveEngordeId).HasColumnName("lote_ave_engorde_id").IsRequired();

        b.Property(x => x.MetrosCuadrados).HasColumnName("metros_cuadrados").HasPrecision(14, 2);
        b.Property(x => x.AvesFinalGranja).HasColumnName("aves_final_granja");
        b.Property(x => x.AvesBeneficiada).HasColumnName("aves_beneficiada");
        b.Property(x => x.ProduccionKiloPie).HasColumnName("produccion_kilo_pie").HasPrecision(16, 2);
        b.Property(x => x.DiasEngorde).HasColumnName("dias_engorde");
        b.Property(x => x.DiasEnGranja).HasColumnName("dias_en_granja");

        b.Property(x => x.RegistradoPorUserId).HasColumnName("registrado_por_user_id").HasMaxLength(64);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // Una liquidación por lote.
        b.HasIndex(x => x.LoteAveEngordeId)
            .IsUnique()
            .HasDatabaseName("ux_liquidacion_lote_engorde_panama_lote");

        b.HasOne(x => x.LoteAveEngorde)
            .WithMany()
            .HasForeignKey(x => x.LoteAveEngordeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
