using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class CompanyMenuConfiguration : IEntityTypeConfiguration<CompanyMenu>
{
    public void Configure(EntityTypeBuilder<CompanyMenu> builder)
    {
        builder.ToTable("company_menus");
        builder.HasKey(x => new { x.CompanyId, x.MenuId });

        builder.Property(x => x.IsEnabled).HasDefaultValue(true);
        builder.Property(x => x.SortOrder).HasDefaultValue(0);
        builder.Property(x => x.ParentMenuId);

        builder.HasOne(x => x.Company)
            .WithMany(c => c.CompanyMenus)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Menu)
            .WithMany(m => m.CompanyMenus)
            .HasForeignKey(x => x.MenuId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
