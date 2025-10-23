using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class RoleCompanyConfiguration : IEntityTypeConfiguration<RoleCompany>
{
    public void Configure(EntityTypeBuilder<RoleCompany> e)
    {
        e.ToTable("role_companies");

        e.HasKey(rc => new { rc.RoleId, rc.CompanyId });

        e.HasOne(rc => rc.Role)
         .WithMany(r => r.RoleCompanies)
         .HasForeignKey(rc => rc.RoleId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(rc => rc.Company)
         .WithMany(c => c.RoleCompanies)
         .HasForeignKey(rc => rc.CompanyId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
