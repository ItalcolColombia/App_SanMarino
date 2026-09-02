// src/ZooSanMarino.Infrastructure/Persistence/Configurations/ItemInventarioConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ItemInventarioConfiguration : IEntityTypeConfiguration<ItemInventario>
{
    public void Configure(EntityTypeBuilder<ItemInventario> e)
    {
        e.ToTable("item_inventario", "public");

        // Nombre FIJO: estas tablas las creo SQL crudo, no EF, asi que sus PK/FK/indices
        // llevan nombres cortos que NO coinciden con los que EF deriva. Sin fijarlos, el
        // rename de la tabla haria que la proxima migracion generada intentara renombrar
        // objetos que en la base no existen con ese nombre — y eso revienta al aplicarse.
        e.HasKey(x => x.Id).HasName("item_inventario_pkey");
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
            .HasDatabaseName("uq_item_inv_company_pais_codigo")
            .IsUnique();
        e.HasIndex(x => x.TipoItem).HasDatabaseName("ix_item_inventario_tipo_item");

        e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId)
            .HasConstraintName("fk_item_inv_company").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Pais).WithMany().HasForeignKey(x => x.PaisId)
            .HasConstraintName("fk_item_inv_pais").OnDelete(DeleteBehavior.Restrict);
    }
}
