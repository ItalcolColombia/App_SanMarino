using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class GuiaGeneticaEcuadorHeaderConfiguration : IEntityTypeConfiguration<GuiaGeneticaEcuadorHeader>
{
    public void Configure(EntityTypeBuilder<GuiaGeneticaEcuadorHeader> e)
    {
        e.ToTable("guia_genetica_ecuador_header");

        e.HasKey(x => x.Id);

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

        e.HasIndex(x => new { x.CompanyId, x.Raza, x.AnioGuia }).IsUnique();

        e.HasMany(x => x.Detalles)
            .WithOne(d => d.GuiaGeneticaEcuadorHeader)
            .HasForeignKey(d => d.GuiaGeneticaEcuadorHeaderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

