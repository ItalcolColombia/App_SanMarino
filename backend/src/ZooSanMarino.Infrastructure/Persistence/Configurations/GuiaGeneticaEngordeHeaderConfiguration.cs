using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class GuiaGeneticaEngordeHeaderConfiguration : IEntityTypeConfiguration<GuiaGeneticaEngordeHeader>
{
    public void Configure(EntityTypeBuilder<GuiaGeneticaEngordeHeader> e)
    {
        e.ToTable("guia_genetica_header");

        e.HasKey(x => x.Id).HasName("guia_genetica_header_pkey");

        e.Property(x => x.PaisId)
            .HasColumnName("pais_id")
            .HasDefaultValue(0)
            .IsRequired();

        e.Property(x => x.Raza)
            .HasColumnName("raza")
            .HasMaxLength(120)
            .IsRequired();

        e.Property(x => x.AnioGuia)
            .HasColumnName("anio_guia")
            .IsRequired();

        e.Property(x => x.Estado)
            .HasColumnName("estado")
            .HasMaxLength(20)
            .HasDefaultValue("active")
            .IsRequired();

        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        e.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired(false);
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired(false);
        e.Property(x => x.DeletedAt).HasColumnName("deleted_at").IsRequired(false);

        e.HasIndex(x => new { x.CompanyId, x.PaisId, x.Raza, x.AnioGuia })
            .HasDatabaseName("ix_guia_genetica_header_company_id_pais_id_raza_anio_g").IsUnique();

        e.HasMany(x => x.Detalles)
            .WithOne(d => d.GuiaGeneticaEngordeHeader)
            .HasForeignKey(d => d.GuiaGeneticaEngordeHeaderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

