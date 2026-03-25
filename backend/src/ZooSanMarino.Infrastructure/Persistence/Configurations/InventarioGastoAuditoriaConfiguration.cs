using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class InventarioGastoAuditoriaConfiguration : IEntityTypeConfiguration<InventarioGastoAuditoria>
{
    public void Configure(EntityTypeBuilder<InventarioGastoAuditoria> e)
    {
        e.ToTable("inventario_gasto_auditoria", "public");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.InventarioGastoId).HasColumnName("inventario_gasto_id").IsRequired();
        e.Property(x => x.Accion).HasColumnName("accion").HasMaxLength(20).IsRequired();
        e.Property(x => x.Fecha).HasColumnName("fecha").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        e.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(128).IsRequired();
        e.Property(x => x.Detalle).HasColumnName("detalle").HasColumnType("text");

        e.HasIndex(x => x.InventarioGastoId).HasDatabaseName("ix_inventario_gasto_auditoria_gasto");
        e.HasIndex(x => x.Accion).HasDatabaseName("ix_inventario_gasto_auditoria_accion");

        e.HasOne(x => x.InventarioGasto).WithMany().HasForeignKey(x => x.InventarioGastoId).OnDelete(DeleteBehavior.Cascade);
    }
}

