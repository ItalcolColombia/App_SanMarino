using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class InventarioGastoConfiguration : IEntityTypeConfiguration<InventarioGasto>
{
    public void Configure(EntityTypeBuilder<InventarioGasto> e)
    {
        e.ToTable("inventario_gasto", "public");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        e.Property(x => x.PaisId).HasColumnName("pais_id").IsRequired();

        e.Property(x => x.FarmId).HasColumnName("farm_id").IsRequired();
        e.Property(x => x.NucleoId).HasColumnName("nucleo_id").HasMaxLength(50);
        e.Property(x => x.GalponId).HasColumnName("galpon_id").HasMaxLength(50);
        e.Property(x => x.LoteAveEngordeId).HasColumnName("lote_ave_engorde_id");

        e.Property(x => x.Fecha).HasColumnName("fecha").HasColumnType("date").IsRequired();
        e.Property(x => x.Observaciones).HasColumnName("observaciones").HasMaxLength(1000);
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).HasDefaultValue("Activo").IsRequired();

        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(128);
        e.Property(x => x.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamptz");
        e.Property(x => x.DeletedByUserId).HasColumnName("deleted_by_user_id").HasMaxLength(128);

        e.HasIndex(x => new { x.CompanyId, x.PaisId, x.FarmId, x.Fecha }).HasDatabaseName("ix_inventario_gasto_company_pais_farm_fecha");
        e.HasIndex(x => x.Estado).HasDatabaseName("ix_inventario_gasto_estado");

        e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Pais).WithMany().HasForeignKey(x => x.PaisId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.LoteAveEngorde).WithMany().HasForeignKey(x => x.LoteAveEngordeId).OnDelete(DeleteBehavior.Restrict);

        e.HasMany(x => x.Detalles).WithOne(d => d.InventarioGasto).HasForeignKey(d => d.InventarioGastoId);
    }
}

