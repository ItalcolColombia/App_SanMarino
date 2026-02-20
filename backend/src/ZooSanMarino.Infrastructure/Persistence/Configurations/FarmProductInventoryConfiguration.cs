// src/ZooSanMarino.Infrastructure/Persistence/Configurations/FarmProductInventoryConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class FarmProductInventoryConfiguration : IEntityTypeConfiguration<FarmProductInventory>
{
    public void Configure(EntityTypeBuilder<FarmProductInventory> e)
    {
        e.ToTable("farm_product_inventory", "public");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.FarmId).HasColumnName("farm_id").IsRequired();
        e.Property(x => x.CatalogItemId).HasColumnName("catalog_item_id").IsRequired();
        
        // Empresa y País
        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        e.Property(x => x.PaisId).HasColumnName("pais_id").IsRequired();

        e.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(18, 3)
            .IsRequired();

        e.Property(x => x.Unit)
            .HasColumnName("unit")
            .HasMaxLength(20)
            .HasDefaultValue("kg")
            .IsRequired();

        e.Property(x => x.Location)
            .HasColumnName("location")
            .HasMaxLength(200);

        e.Property(x => x.LotNumber)
            .HasColumnName("lot_number")
            .HasMaxLength(50);

        e.Property(x => x.ExpirationDate)
            .HasColumnName("expiration_date")
            .HasColumnType("timestamptz");

        e.Property(x => x.UnitCost)
            .HasColumnName("unit_cost")
            .HasPrecision(18, 2);

        e.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .IsRequired();

        e.Property(x => x.Active)
            .HasColumnName("active")
            .HasDefaultValue(true)
            .IsRequired();

        e.Property(x => x.ResponsibleUserId)
            .HasColumnName("responsible_user_id")
            .HasMaxLength(128);

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

        // Índices
        e.HasIndex(x => new { x.FarmId, x.CatalogItemId })
            .HasDatabaseName("ix_farm_product_inventory_farm_catalog_item")
            .IsUnique();

        e.HasIndex(x => x.CompanyId)
            .HasDatabaseName("ix_farm_product_inventory_company_id");

        e.HasIndex(x => x.PaisId)
            .HasDatabaseName("ix_farm_product_inventory_pais_id");

        e.HasIndex(x => x.CatalogItemId)
            .HasDatabaseName("ix_farm_product_inventory_catalog_item_id");

        // Relaciones
        e.HasOne(x => x.Farm)
            .WithMany()
            .HasForeignKey(x => x.FarmId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // No configurar navegación explícita para evitar JOINs automáticos
        // La relación se maneja solo por la foreign key

        e.HasOne(x => x.CatalogItem)
            .WithMany()
            .HasForeignKey(x => x.CatalogItemId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Pais)
            .WithMany()
            .HasForeignKey(x => x.PaisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
