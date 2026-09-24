using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ValidacionProcesoConfiguration : IEntityTypeConfiguration<ValidacionProceso>
{
    public void Configure(EntityTypeBuilder<ValidacionProceso> e)
    {
        e.ToTable("validacion_procesos", "public");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.Key).HasColumnName("key").HasMaxLength(60).IsRequired();
        e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(120).IsRequired();
        e.Property(x => x.Descripcion).HasColumnName("descripcion");
        e.Property(x => x.AdapterKey).HasColumnName("adapter_key").HasMaxLength(60).IsRequired();
        e.Property(x => x.MenuRoute).HasColumnName("menu_route").HasMaxLength(200);
        e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        e.Property(x => x.Orden).HasColumnName("orden").HasDefaultValue(0).IsRequired();

        e.HasIndex(x => x.Key).IsUnique().HasDatabaseName("ux_validacion_procesos_key");
    }
}
