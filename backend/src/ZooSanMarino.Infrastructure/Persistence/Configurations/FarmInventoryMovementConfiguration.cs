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
        
        // Tipo de item del catálogo
        e.Property(x => x.ItemType)
            .HasColumnName("item_type")
            .HasMaxLength(50);
        
        // Empresa y País
        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        e.Property(x => x.PaisId).HasColumnName("pais_id").IsRequired();

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
        
        // Campos específicos para movimiento de alimento
        e.Property(x => x.DocumentoOrigen)
            .HasColumnName("documento_origen")
            .HasMaxLength(50);
            
        e.Property(x => x.TipoEntrada)
            .HasColumnName("tipo_entrada")
            .HasMaxLength(50);
            
        e.Property(x => x.GalponDestinoId)
            .HasColumnName("galpon_destino_id")
            .HasMaxLength(50);
            
        e.Property(x => x.FechaMovimiento)
            .HasColumnName("fecha_movimiento")
            .HasColumnType("timestamptz");
        
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
        e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_farm_inventory_movements_company_id");
        e.HasIndex(x => x.PaisId).HasDatabaseName("ix_farm_inventory_movements_pais_id");
        e.HasIndex(x => x.ItemType).HasDatabaseName("ix_farm_inventory_movements_item_type");
        e.HasIndex(x => x.DocumentoOrigen).HasDatabaseName("ix_farm_inventory_movements_documento_origen");
        e.HasIndex(x => x.TipoEntrada).HasDatabaseName("ix_farm_inventory_movements_tipo_entrada");
        e.HasIndex(x => x.FechaMovimiento).HasDatabaseName("ix_farm_inventory_movements_fecha_movimiento");

        e.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.CatalogItem).WithMany().HasForeignKey(x => x.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Pais).WithMany().HasForeignKey(x => x.PaisId).OnDelete(DeleteBehavior.Cascade);
    }
}
