// src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteSiloConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LoteSiloConfiguration : IEntityTypeConfiguration<LoteSilo>
{
    public void Configure(EntityTypeBuilder<LoteSilo> b)
    {
        b.ToTable("lote_silos", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.LoteId).HasColumnName("lote_id").IsRequired();
        b.Property(x => x.FarmSiloId).HasColumnName("farm_silo_id").IsRequired();

        b.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired(false);

        // Cascade: si el lote desaparece, su asignación de silos no tiene sentido. El histórico de
        // consumo NO depende de esta tabla (cada movimiento guarda su propio silo_id).
        b.HasOne<Lote>()
            .WithMany()
            .HasForeignKey(x => x.LoteId)
            .HasConstraintName("fk_lote_silos_lote")
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.FarmSilo)
            .WithMany()
            .HasForeignKey(x => x.FarmSiloId)
            .HasConstraintName("fk_lote_silos_silo")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.LoteId, x.FarmSiloId })
            .IsUnique()
            .HasDatabaseName("ux_lote_silos_lote_silo");

        b.HasIndex(x => x.FarmSiloId).HasDatabaseName("ix_lote_silos_silo");
    }
}
