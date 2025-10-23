using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.ToTable("roles");

            builder.HasKey(r => r.Id);

            builder.Property(r => r.Name)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.HasIndex(r => r.Name).IsUnique(); // opcional: unicidad por nombre

            builder.Property(r => r.Description)
                   .HasMaxLength(250);

            // Role -> RolePermissions
            builder.HasMany(r => r.RolePermissions)
                   .WithOne(rp => rp.Role)
                   .HasForeignKey(rp => rp.RoleId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Role -> UserRoles
            builder.HasMany(r => r.UserRoles)
                   .WithOne(ur => ur.Role)
                   .HasForeignKey(ur => ur.RoleId)
                   .OnDelete(DeleteBehavior.Cascade);

            // ✅ Role -> RoleCompanies (usar navegación, NO HasMany<RoleCompany>() pelado)
            builder.HasMany(r => r.RoleCompanies)
                   .WithOne(rc => rc.Role)
                   .HasForeignKey(rc => rc.RoleId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
