// src/ZooSanMarino.Infrastructure/Persistence/Configurations/MasterListConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class MasterListConfiguration : IEntityTypeConfiguration<MasterList>
{
    public void Configure(EntityTypeBuilder<MasterList> e)
    {
        e.ToTable("master_lists");
        e.HasKey(x => x.Id);
        
        // El índice único de Key ahora debe considerar CompanyId y CountryId
        // Permitir mismo Key para diferentes compañías/países
        e.HasIndex(x => new { x.Key, x.CompanyId, x.CountryId })
         .IsUnique()
         .HasFilter("company_id IS NOT NULL AND country_id IS NOT NULL");
        
        // También mantener índice único para Key solo (para compatibilidad con registros antiguos)
        e.HasIndex(x => x.Key)
         .IsUnique()
         .HasFilter("company_id IS NULL AND country_id IS NULL");
        
        e.Property(x => x.Key)
         .HasMaxLength(100)
         .IsRequired();
        e.Property(x => x.Name)
         .HasMaxLength(200)
         .IsRequired();

        // Nuevas propiedades
        e.Property(x => x.CompanyId)
         .HasColumnName("company_id")
         .IsRequired(false);
        
        e.Property(x => x.CompanyName)
         .HasColumnName("company_name")
         .HasMaxLength(200)
         .IsRequired(false);
        
        e.Property(x => x.CountryId)
         .HasColumnName("country_id")
         .IsRequired(false);
        
        e.Property(x => x.CountryName)
         .HasColumnName("country_name")
         .HasMaxLength(200)
         .IsRequired(false);

        e.HasMany(x => x.Options)
         .WithOne(o => o.MasterList)
         .HasForeignKey(o => o.MasterListId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
