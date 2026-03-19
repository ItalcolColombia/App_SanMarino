// src/ZooSanMarino.Infrastructure/Persistence/Configurations/ItemInventarioEcuadorConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ItemInventarioEcuadorConfiguration : IEntityTypeConfiguration<ItemInventarioEcuador>
{
    public void Configure(EntityTypeBuilder<ItemInventarioEcuador> e)
    {
        e.ToTable("item_inventario_ecuador", "public");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(50).IsRequired();
        e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
        e.Property(x => x.TipoItem).HasColumnName("tipo_item").HasMaxLength(50).IsRequired();
        e.Property(x => x.Unidad).HasColumnName("unidad").HasMaxLength(20).HasDefaultValue("kg").IsRequired();
        e.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(500);
        e.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true).IsRequired();

        e.Property(x => x.Grupo).HasColumnName("grupo").HasMaxLength(100);
        e.Property(x => x.TipoInventarioCodigo).HasColumnName("tipo_inventario_codigo").HasMaxLength(50);
        e.Property(x => x.DescripcionTipoInventario).HasColumnName("descripcion_tipo_inventario").HasMaxLength(200);
        e.Property(x => x.Referencia).HasColumnName("referencia").HasMaxLength(100);
        e.Property(x => x.DescripcionItem).HasColumnName("descripcion_item").HasMaxLength(500);
        e.Property(x => x.Concepto).HasColumnName("concepto").HasMaxLength(200);

        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        e.Property(x => x.PaisId).HasColumnName("pais_id").IsRequired();

        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        e.HasIndex(x => new { x.CompanyId, x.PaisId, x.Codigo })
            .HasDatabaseName("ix_item_inventario_ecuador_company_pais_codigo")
            .IsUnique();
        e.HasIndex(x => x.TipoItem).HasDatabaseName("ix_item_inventario_ecuador_tipo_item");

        e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Pais).WithMany().HasForeignKey(x => x.PaisId).OnDelete(DeleteBehavior.Restrict);
    }
}
