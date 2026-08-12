// src/ZooSanMarino.Infrastructure/Persistence/Configurations/SiloCatalogoConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class SiloCatalogoConfiguration : IEntityTypeConfiguration<SiloCatalogo>
{
    public void Configure(EntityTypeBuilder<SiloCatalogo> b)
    {
        b.ToTable("silo_catalogo", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.Numero).HasColumnName("numero").IsRequired();

        b.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(120)
            .IsRequired();

        b.Property(x => x.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(200)
            .IsRequired(false);

        b.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired(false);
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at").IsRequired(false);

        b.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .HasConstraintName("fk_silo_catalogo_company")
            .OnDelete(DeleteBehavior.Restrict);

        // Únicos entre los NO borrados: el número y el nombre identifican al silo dentro de la
        // empresa, pero una baja lógica no puede bloquear volver a usar ese número.
        b.HasIndex(x => new { x.CompanyId, x.Numero })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ux_silo_catalogo_company_numero");

        b.HasIndex(x => new { x.CompanyId, x.Nombre })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ux_silo_catalogo_company_nombre");
    }
}
