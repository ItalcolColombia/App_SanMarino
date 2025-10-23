// src/ZooSanMarino.Infrastructure/Persistence/Configurations/RoleMenuConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class RoleMenuConfiguration : IEntityTypeConfiguration<RoleMenu>
{
    public void Configure(EntityTypeBuilder<RoleMenu> e)
    {
        e.ToTable("role_menus");
        e.HasKey(x => new { x.RoleId, x.MenuId });

        e.HasOne(x => x.Role)
         .WithMany(r => r.RoleMenus)
         .HasForeignKey(x => x.RoleId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Menu)
         .WithMany(m => m.RoleMenus)
         .HasForeignKey(x => x.MenuId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
