using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> b)
    {
        b.ToTable("menus");
        b.HasKey(x => x.Id);
        b.Property(x => x.Label).IsRequired().HasMaxLength(120);
        b.Property(x => x.Icon).HasMaxLength(60);
        b.Property(x => x.Route).HasMaxLength(200);

        b.HasOne(x => x.Parent)
         .WithMany(x => x.Children)
         .HasForeignKey(x => x.ParentId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.ParentId, x.Order });

        b.HasMany(x => x.MenuPermissions)
         .WithOne(mp => mp.Menu)
         .HasForeignKey(mp => mp.MenuId);
    }
}
