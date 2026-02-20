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

        e.Property(x => x.ItemType)
         .HasColumnName("item_type")
         .HasMaxLength(50)
         .HasDefaultValue("alimento")
         .IsRequired();

        e.Property(x => x.Metadata)
         .HasColumnName("metadata")
         .HasColumnType("jsonb")
         .IsRequired();

        e.Property(x => x.Activo)
         .HasColumnName("activo")
         .HasDefaultValue(true)
         .IsRequired();

        e.Property(x => x.CompanyId)
         .HasColumnName("company_id")
         .IsRequired();

        e.Property(x => x.PaisId)
         .HasColumnName("pais_id")
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

        // Índice único compuesto: código debe ser único por empresa y país
        e.HasIndex(x => new { x.CompanyId, x.PaisId, x.Codigo })
         .HasDatabaseName("ux_catalogo_items_codigo_company_pais")
         .IsUnique();
        e.HasIndex(x => x.Activo).HasDatabaseName("ix_catalogo_items_activo");
        e.HasIndex(x => x.Nombre).HasDatabaseName("ix_catalogo_items_nombre");
        e.HasIndex(x => x.ItemType).HasDatabaseName("ix_catalogo_items_item_type");
        e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_catalogo_items_company_id");
        e.HasIndex(x => x.PaisId).HasDatabaseName("ix_catalogo_items_pais_id");
        e.HasIndex(x => new { x.CompanyId, x.PaisId }).HasDatabaseName("ix_catalogo_items_company_pais");
        e.HasIndex(x => new { x.CompanyId, x.Activo }).HasDatabaseName("ix_catalogo_items_company_activo");
        e.HasIndex(x => new { x.CompanyId, x.ItemType }).HasDatabaseName("ix_catalogo_items_company_type");
        e.HasIndex(x => new { x.CompanyId, x.ItemType, x.Activo }).HasDatabaseName("ix_catalogo_items_company_type_activo");
    }
}
