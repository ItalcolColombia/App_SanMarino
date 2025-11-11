// src/ZooSanMarino.Infrastructure/Persistence/Configurations/PaisConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class PaisConfiguration : IEntityTypeConfiguration<Pais>
{
    public void Configure(EntityTypeBuilder<Pais> e)
    {
        e.ToTable("paises");
        e.HasKey(x => x.PaisId);
        e.Property(x => x.PaisNombre)
         .HasMaxLength(200)
         .IsRequired();

        // Deshabilitar temporalmente relación con UserCompanies para evitar FK sombra (pais_id1)
        e.Ignore(x => x.UserCompanies);
    }
}
