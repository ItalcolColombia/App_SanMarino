// src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteHuevoItemConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LoteHuevoItemConfiguration : IEntityTypeConfiguration<LoteHuevoItem>
{
    public void Configure(EntityTypeBuilder<LoteHuevoItem> b)
    {
        b.ToTable("lote_huevo_items", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.LoteId).HasColumnName("lote_id").IsRequired();
        b.Property(x => x.CatalogItemId).HasColumnName("catalog_item_id").IsRequired();

        b.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired(false);

        // Cascade: si el lote desaparece, la declaración de qué producía no tiene sentido. El
        // histórico NO depende de esta tabla — cada seguimiento guarda su propio desglose en
        // metadata.huevoItems, y esa foto es la que leen indicadores, reportes y traslados.
        b.HasOne<Lote>()
            .WithMany()
            .HasForeignKey(x => x.LoteId)
            .HasConstraintName("fk_lote_huevo_items_lote")
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: un ítem del catálogo que algún lote declara no se puede borrar de la base. Para
        // sacarlo de circulación está `activo`, que es el camino que ya usa el CRUD de catálogo.
        b.HasOne(x => x.CatalogItem)
            .WithMany()
            .HasForeignKey(x => x.CatalogItemId)
            .HasConstraintName("fk_lote_huevo_items_item")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.LoteId, x.CatalogItemId })
            .IsUnique()
            .HasDatabaseName("ux_lote_huevo_items_lote_item");

        b.HasIndex(x => x.CatalogItemId).HasDatabaseName("ix_lote_huevo_items_item");
    }
}
