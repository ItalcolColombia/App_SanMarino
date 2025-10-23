using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class FarmConfiguration : IEntityTypeConfiguration<Farm>
{
    public void Configure(EntityTypeBuilder<Farm> e)
    {
        e.ToTable("farms");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("integer");

        e.Property(x => x.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        e.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        // RegionalId ahora opcional (front no lo envía en create)
        e.Property(x => x.RegionalId)
            .HasColumnName("regional_id")
            .IsRequired(false);

        // Status como 'A'/'I' (1 char) y default 'A'
        e.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(1)
            .HasDefaultValue("A")
            .IsRequired();

        // Cascada País → Departamento → Municipio
        e.Property(x => x.DepartamentoId)
            .HasColumnName("departamento_id")
            .IsRequired();

        e.Property(x => x.MunicipioId)
            .HasColumnName("municipio_id")
            .IsRequired();

        // Auditoría
        e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        e.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        // FKs maestras (sin borrar en cascada)
        e.HasOne<Departamento>()
            .WithMany()
            .HasForeignKey(x => x.DepartamentoId)
            .HasConstraintName("fk_farms_departamento")
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne<Municipio>()
            .WithMany()
            .HasForeignKey(x => x.MunicipioId)
            .HasConstraintName("fk_farms_municipio")
            .OnDelete(DeleteBehavior.Restrict);

        // Índices
        e.HasIndex(x => new { x.CompanyId, x.Name })
            .HasDatabaseName("ux_farms_company_name")
            .IsUnique();                 // ← evita duplicados por compañía+nombre

        e.HasIndex(x => x.DepartamentoId);
        e.HasIndex(x => x.MunicipioId);

        // Relación con Galpon (ya la tenías)
        e.HasMany(f => f.Galpones)
            .WithOne(g => g.Farm)
            .HasForeignKey(g => g.GranjaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
