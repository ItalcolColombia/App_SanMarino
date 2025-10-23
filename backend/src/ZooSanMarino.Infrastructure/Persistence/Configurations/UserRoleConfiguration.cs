// src/ZooSanMarino.Infrastructure/Persistence/Configurations/UserRoleConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> e)
    {
        e.ToTable("user_roles");
        e.HasKey(x => new { x.UserId, x.RoleId });

        e.HasOne(x => x.User)
         .WithMany(u => u.UserRoles)
         .HasForeignKey(x => x.UserId);

        e.HasOne(x => x.Role)
         .WithMany(r => r.UserRoles)
         .HasForeignKey(x => x.RoleId);
    }
}
