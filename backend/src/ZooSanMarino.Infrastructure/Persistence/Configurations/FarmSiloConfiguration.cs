// src/ZooSanMarino.Infrastructure/Persistence/Configurations/FarmSiloConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class FarmSiloConfiguration : IEntityTypeConfiguration<FarmSilo>
{
    public void Configure(EntityTypeBuilder<FarmSilo> b)
    {
        b.ToTable("farm_silos", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.GranjaId).HasColumnName("granja_id").IsRequired();

        b.Property(x => x.SiloCatalogoId)
            .HasColumnName("silo_catalogo_id")
            .IsRequired(false);

        b.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(120)
            .IsRequired();

        // 'Silo' (alimento) | 'Insumos' (bodega)
        b.Property(x => x.Tipo)
            .HasColumnName("tipo")
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.CodigoErpUbicacion)
            .HasColumnName("codigo_erp_ubicacion")
            .HasMaxLength(20)
            .IsRequired(false);

        b.Property(x => x.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(200)
            .IsRequired(false);

        b.Property(x => x.CentroOperacion)
            .HasColumnName("centro_operacion")
            .HasMaxLength(20)
            .IsRequired(false);

        b.Property(x => x.CodigoBodega)
            .HasColumnName("codigo_bodega")
            .HasMaxLength(20)
            .IsRequired(false);

        b.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired(false);
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at").IsRequired(false);

        // FK a la granja (sin cascada: el catálogo se limpia explícitamente)
        b.HasOne<Farm>()
            .WithMany()
            .HasForeignKey(x => x.GranjaId)
            .HasConstraintName("fk_farm_silos_farm")
            .OnDelete(DeleteBehavior.Restrict);

        // FK a la lista maestra (nullable: las bodegas no salen del catálogo)
        b.HasOne<SiloCatalogo>()
            .WithMany()
            .HasForeignKey(x => x.SiloCatalogoId)
            .HasConstraintName("fk_farm_silos_silo_catalogo")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.GranjaId).HasDatabaseName("ix_farm_silos_granja");

        // Filtrado por deleted_at: al ganar baja lógica, un silo borrado ya no puede bloquear el
        // alta de otro con el mismo nombre. Hoy todas las filas tienen deleted_at NULL, así que el
        // filtro no cambia qué colisiona — solo evita el falso conflicto a futuro.
        b.HasIndex(x => new { x.GranjaId, x.Nombre })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ux_farm_silos_granja_nombre");
    }
}
