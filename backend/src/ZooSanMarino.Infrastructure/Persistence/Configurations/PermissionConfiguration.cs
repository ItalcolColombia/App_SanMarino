// src/ZooSanMarino.Infrastructure/Persistence/Configurations/PermissionConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Key)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(p => p.Description)
               .HasMaxLength(250);

        builder.HasIndex(p => p.Key).IsUnique();
    }
}
