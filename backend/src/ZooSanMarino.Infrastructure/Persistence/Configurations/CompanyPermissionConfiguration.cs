using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class CompanyPermissionConfiguration : IEntityTypeConfiguration<CompanyPermission>
{
    public void Configure(EntityTypeBuilder<CompanyPermission> builder)
    {
        builder.ToTable("company_permissions");
        builder.HasKey(x => new { x.CompanyId, x.PermissionId });

        builder.Property(x => x.IsEnabled).HasDefaultValue(true);

        builder.HasOne(x => x.Company)
            .WithMany(c => c.CompanyPermissions)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Permission)
            .WithMany(p => p.CompanyPermissions)
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
