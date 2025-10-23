using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class MenuPermissionConfiguration : IEntityTypeConfiguration<MenuPermission>
{
    public void Configure(EntityTypeBuilder<MenuPermission> e)
    {
        e.ToTable("menu_permissions");
        e.HasKey(x => new { x.MenuId, x.PermissionId });

        e.HasOne(x => x.Permission)
         .WithMany(p => p.MenuPermissions)      // opcional: agrega ICollection<MenuPermission> en Permission si deseas navegar
         .HasForeignKey(x => x.PermissionId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
