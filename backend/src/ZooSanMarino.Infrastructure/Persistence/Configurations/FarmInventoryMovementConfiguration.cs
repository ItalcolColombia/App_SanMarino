// src/ZooSanMarino.Infrastructure/Persistence/Configurations/FarmInventoryMovementConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Domain.Enums;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class FarmInventoryMovementConfiguration : IEntityTypeConfiguration<FarmInventoryMovement>
{
    public void Configure(EntityTypeBuilder<FarmInventoryMovement> e)
    {
        e.ToTable("farm_inventory_movements", "public");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.FarmId).HasColumnName("farm_id").IsRequired();
        e.Property(x => x.CatalogItemId).HasColumnName("catalog_item_id").IsRequired();

        e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18,3).IsRequired();

        e.Property(x => x.MovementType)
         .HasColumnName("movement_type")
         .HasConversion(
             v => v.ToString(),
             v => Enum.Parse<InventoryMovementType>(v))
         .HasMaxLength(20)
         .IsRequired();

        e.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(20).HasDefaultValue("kg").IsRequired();
        e.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(50);
        e.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(200);
        
        // Campos nuevos: origin y destination
        // IMPORTANTE: Establecer explícitamente como opcionales y con nombres exactos en minúsculas
        e.Property(x => x.Origin)
            .HasColumnName("origin")  // Nombre exacto en minúsculas
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired(false);
            
        e.Property(x => x.Destination)
            .HasColumnName("destination")  // Nombre exacto en minúsculas
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired(false);
            
        e.Property(x => x.TransferGroupId).HasColumnName("transfer_group_id");
        e.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb").IsRequired();
        e.Property(x => x.ResponsibleUserId).HasColumnName("responsible_user_id").HasMaxLength(128);

        e.Property(x => x.CreatedAt)
         .HasColumnName("created_at")
         .HasColumnType("timestamptz")
         .HasDefaultValueSql("now()")
         .ValueGeneratedOnAdd();

        e.HasIndex(x => new { x.FarmId, x.CatalogItemId }).HasDatabaseName("ix_fim_farm_item");
        e.HasIndex(x => x.MovementType).HasDatabaseName("ix_fim_type");
        e.HasIndex(x => x.TransferGroupId).HasDatabaseName("ix_fim_transfer_group");

        e.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.CatalogItem).WithMany().HasForeignKey(x => x.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
