// file: src/ZooSanMarino.Infrastructure/Persistence/Configurations/NucleoConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class NucleoConfiguration : IEntityTypeConfiguration<Nucleo>
{
    public void Configure(EntityTypeBuilder<Nucleo> b)
    {
        // Tabla y schema
        b.ToTable("nucleos", "public"  );

        // PK compuesta natural
        b.HasKey(x => new { x.NucleoId, x.GranjaId });

        // --------- Columns ----------
        b.Property(x => x.NucleoId)
            .HasColumnName("nucleo_id")
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.GranjaId)
            .HasColumnName("granja_id")
            .IsRequired();

        b.Property(x => x.NucleoNombre)
            .HasColumnName("nucleo_nombre")
            .HasMaxLength(150)
            .IsRequired();

        // Si tienes multi-tenant en Nucleo, agrega CompanyId:
        // b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        // --------- Índices ----------
        b.HasIndex(x => new { x.GranjaId, x.NucleoNombre })
            .HasDatabaseName("ix_nucleo_granja_nombre");

        // --------- Relaciones ----------
        b.HasOne(x => x.Farm)
         .WithMany(f => f.Nucleos)
         .HasForeignKey(x => x.GranjaId)
         .OnDelete(DeleteBehavior.Restrict);

        // Si manejas Company en Nucleo:
        // b.HasOne(x => x.Company)
        //  .WithMany()
        //  .HasForeignKey(x => x.CompanyId)
        //  .OnDelete(DeleteBehavior.Restrict);
    }
}
