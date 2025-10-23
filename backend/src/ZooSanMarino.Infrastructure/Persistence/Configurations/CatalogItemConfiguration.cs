using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> e)
    {
        e.ToTable("catalogo_items", "public");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.Codigo)
         .HasColumnName("codigo")
         .HasMaxLength(50)
         .IsRequired();

        e.Property(x => x.Nombre)
         .HasColumnName("nombre")
         .HasMaxLength(200)
         .IsRequired();

        e.Property(x => x.Metadata)
         .HasColumnName("metadata")
         .HasColumnType("jsonb")
         .IsRequired();

        e.Property(x => x.Activo)
         .HasColumnName("activo")
         .HasDefaultValue(true)
         .IsRequired();

        e.Property(x => x.CreatedAt)
         .HasColumnName("created_at")
         .HasColumnType("timestamptz")
         .HasDefaultValueSql("now()")
         .ValueGeneratedOnAdd();

        e.Property(x => x.UpdatedAt)
         .HasColumnName("updated_at")
         .HasColumnType("timestamptz")
         .HasDefaultValueSql("now()")
         .ValueGeneratedOnAddOrUpdate();

        e.HasIndex(x => x.Codigo).HasDatabaseName("ux_catalogo_items_codigo").IsUnique();
        e.HasIndex(x => x.Activo).HasDatabaseName("ix_catalogo_items_activo");
        e.HasIndex(x => x.Nombre).HasDatabaseName("ix_catalogo_items_nombre");
    }
}
