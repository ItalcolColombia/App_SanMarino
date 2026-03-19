// src/ZooSanMarino.Infrastructure/Persistence/Configurations/InventarioGestionMovimientoConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class InventarioGestionMovimientoConfiguration : IEntityTypeConfiguration<InventarioGestionMovimiento>
{
    public void Configure(EntityTypeBuilder<InventarioGestionMovimiento> e)
    {
        e.ToTable("inventario_gestion_movimiento", "public");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        e.Property(x => x.PaisId).HasColumnName("pais_id").IsRequired();
        e.Property(x => x.FarmId).HasColumnName("farm_id").IsRequired();
        e.Property(x => x.NucleoId).HasColumnName("nucleo_id").HasMaxLength(50);
        e.Property(x => x.GalponId).HasColumnName("galpon_id").HasMaxLength(50);
        e.Property(x => x.ItemInventarioEcuadorId).HasColumnName("item_inventario_ecuador_id").IsRequired();

        e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 3).IsRequired();
        e.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(20).HasDefaultValue("kg").IsRequired();
        e.Property(x => x.MovementType).HasColumnName("movement_type").HasMaxLength(30).IsRequired();
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(80);

        e.Property(x => x.FromFarmId).HasColumnName("from_farm_id");
        e.Property(x => x.FromNucleoId).HasColumnName("from_nucleo_id").HasMaxLength(50);
        e.Property(x => x.FromGalponId).HasColumnName("from_galpon_id").HasMaxLength(50);

        e.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(100);
        e.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        e.Property(x => x.TransferGroupId).HasColumnName("transfer_group_id");
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(128);

        e.HasIndex(x => new { x.FarmId, x.ItemInventarioEcuadorId }).HasDatabaseName("ix_igm_farm_item");
        e.HasIndex(x => x.MovementType).HasDatabaseName("ix_igm_movement_type");
        e.HasIndex(x => x.TransferGroupId).HasDatabaseName("ix_igm_transfer_group");
        e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_igm_company_id");
        e.HasIndex(x => x.PaisId).HasDatabaseName("ix_igm_pais_id");

        e.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ItemInventarioEcuador).WithMany().HasForeignKey(x => x.ItemInventarioEcuadorId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Pais).WithMany().HasForeignKey(x => x.PaisId).OnDelete(DeleteBehavior.Restrict);
    }
}
