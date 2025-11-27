using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class CompanyPaisConfiguration : IEntityTypeConfiguration<CompanyPais>
{
    public void Configure(EntityTypeBuilder<CompanyPais> builder)
    {
        builder.ToTable("company_pais");
        
        // Clave primaria compuesta
        builder.HasKey(x => new { x.CompanyId, x.PaisId });
        
        // Relación con Company
        builder.HasOne(x => x.Company)
            .WithMany(c => c.CompanyPaises)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Relación con Pais
        builder.HasOne(x => x.Pais)
            .WithMany(p => p.CompanyPaises)
            .HasForeignKey(x => x.PaisId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Índices para mejorar rendimiento
        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => x.PaisId);
        
        // Campos de auditoría
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
    }
}





