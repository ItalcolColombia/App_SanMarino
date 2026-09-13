using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class PermissionModuleConfiguration : IEntityTypeConfiguration<PermissionModule>
{
    public void Configure(EntityTypeBuilder<PermissionModule> builder)
    {
        builder.ToTable("permission_modules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Orden).HasDefaultValue(0);

        builder.HasIndex(x => x.Key).IsUnique();
    }
}

public class PermissionModulePermissionConfiguration : IEntityTypeConfiguration<PermissionModulePermission>
{
    public void Configure(EntityTypeBuilder<PermissionModulePermission> builder)
    {
        builder.ToTable("permission_module_permissions");
        builder.HasKey(x => new { x.ModuleId, x.PermissionId });

        builder.HasOne(x => x.Module)
            .WithMany(m => m.Permisos)
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Permission)
            .WithMany(p => p.PermissionModulePermissions)
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CompanyPermissionModuleConfiguration : IEntityTypeConfiguration<CompanyPermissionModule>
{
    public void Configure(EntityTypeBuilder<CompanyPermissionModule> builder)
    {
        builder.ToTable("company_permission_modules");
        builder.HasKey(x => new { x.CompanyId, x.ModuleId });

        builder.Property(x => x.IsEnabled).HasDefaultValue(true);

        builder.HasOne(x => x.Company)
            .WithMany(c => c.CompanyPermissionModules)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Module)
            .WithMany(m => m.Empresas)
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
