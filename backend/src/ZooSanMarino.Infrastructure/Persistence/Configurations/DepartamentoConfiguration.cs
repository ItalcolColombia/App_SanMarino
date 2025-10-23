using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class DepartamentoConfiguration : IEntityTypeConfiguration<Departamento>
{
    public void Configure(EntityTypeBuilder<Departamento> builder)
    {
        builder.ToTable("departamentos");

        builder.HasKey(d => d.DepartamentoId)
               .HasName("pk_departamento");

        builder.Property(d => d.DepartamentoId)
               .HasColumnName("departamento_id")
               .ValueGeneratedOnAdd();

        builder.Property(d => d.DepartamentoNombre)
               .HasColumnName("departamento_nombre")
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(d => d.PaisId)
               .HasColumnName("pais_id")
               .IsRequired();

        builder.Property(d => d.Active)
               .HasColumnName("active")
               .HasDefaultValue(true);

        builder.HasOne(d => d.Pais)
               .WithMany(p => p.Departamentos)
               .HasForeignKey(d => d.PaisId)
               .OnDelete(DeleteBehavior.Restrict); // ← no cascada para no romper referencias

        builder.HasIndex(d => new { d.PaisId, d.DepartamentoNombre })
               .HasDatabaseName("ux_departamentos_pais_nombre")
               .IsUnique();                         // ← único por país+nombre
    }
}
