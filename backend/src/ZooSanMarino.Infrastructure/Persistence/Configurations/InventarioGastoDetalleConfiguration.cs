using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class InventarioGastoDetalleConfiguration : IEntityTypeConfiguration<InventarioGastoDetalle>
{
    public void Configure(EntityTypeBuilder<InventarioGastoDetalle> e)
    {
        e.ToTable("inventario_gasto_detalle", "public");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.InventarioGastoId).HasColumnName("inventario_gasto_id").IsRequired();
        e.Property(x => x.ItemInventarioEcuadorId).HasColumnName("item_inventario_ecuador_id").IsRequired();
        e.Property(x => x.Concepto).HasColumnName("concepto").HasMaxLength(200);
        e.Property(x => x.Cantidad).HasColumnName("cantidad").HasPrecision(18, 3).IsRequired();
        e.Property(x => x.Unidad).HasColumnName("unidad").HasMaxLength(20).HasDefaultValue("kg").IsRequired();
        e.Property(x => x.StockAntes).HasColumnName("stock_antes").HasPrecision(18, 3);
        e.Property(x => x.StockDespues).HasColumnName("stock_despues").HasPrecision(18, 3);

        e.HasIndex(x => x.InventarioGastoId).HasDatabaseName("ix_inventario_gasto_detalle_gasto");

        e.HasOne(x => x.InventarioGasto).WithMany(g => g.Detalles).HasForeignKey(x => x.InventarioGastoId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.ItemInventarioEcuador).WithMany().HasForeignKey(x => x.ItemInventarioEcuadorId).OnDelete(DeleteBehavior.Restrict);
    }
}

