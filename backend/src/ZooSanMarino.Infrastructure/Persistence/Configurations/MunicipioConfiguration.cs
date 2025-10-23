using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class MunicipioConfiguration : IEntityTypeConfiguration<Municipio>
{
    public void Configure(EntityTypeBuilder<Municipio> e)
    {
        e.ToTable("municipios");

        e.HasKey(x => x.MunicipioId);
        e.Property(x => x.MunicipioId).HasColumnName("municipio_id");

        e.Property(x => x.MunicipioNombre)
            .HasColumnName("nombre")
            .HasMaxLength(120)
            .IsRequired();

        e.Property(x => x.DepartamentoId)
            .HasColumnName("departamento_id")
            .IsRequired();

        e.HasOne(x => x.Departamento)
            .WithMany()
            .HasForeignKey(x => x.DepartamentoId)
            .HasConstraintName("fk_municipios_departamento")
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => new { x.DepartamentoId, x.MunicipioNombre })
         .HasDatabaseName("ix_municipios_dep_nombre");
    }
}
