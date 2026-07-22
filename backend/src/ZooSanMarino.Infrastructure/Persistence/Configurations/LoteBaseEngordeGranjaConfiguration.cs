// src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteBaseEngordeGranjaConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LoteBaseEngordeGranjaConfiguration : IEntityTypeConfiguration<LoteBaseEngordeGranja>
{
    public void Configure(EntityTypeBuilder<LoteBaseEngordeGranja> b)
    {
        b.ToTable("lote_base_engorde_granja", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.LoteBaseEngordeId).HasColumnName("lote_base_engorde_id").IsRequired();
        b.Property(x => x.FarmId).HasColumnName("farm_id").IsRequired();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");

        // Un lote base no puede repetir la misma granja.
        b.HasIndex(x => new { x.LoteBaseEngordeId, x.FarmId })
            .IsUnique()
            .HasDatabaseName("ux_lote_base_engorde_granja_base_farm");
        b.HasIndex(x => x.FarmId).HasDatabaseName("ix_lote_base_engorde_granja_farm");
    }
}
