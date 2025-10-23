// file: src/ZooSanMarino.Infrastructure/Persistence/Configurations/GalponConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class GalponConfiguration : IEntityTypeConfiguration<Galpon>
{
    public void Configure(EntityTypeBuilder<Galpon> b)
    {
        // Tabla y schema (ajusta a "galpones" si tu BD ya existe así)
        b.ToTable("galpones", "public");

        // PK
        b.HasKey(x => x.GalponId);

        // --------- Columns ----------
        b.Property(x => x.GalponId)
            .HasColumnName("galpon_id")
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.GalponNombre)
            .HasColumnName("galpon_nombre")
            .HasMaxLength(200)
            .IsRequired();

        b.Property(x => x.GranjaId)
            .HasColumnName("granja_id")
            .IsRequired();

        b.Property(x => x.NucleoId)
            .HasColumnName("nucleo_id")
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        // Si en tu entidad son strings; si fueran decimales usa HasPrecision
        b.Property(x => x.Ancho)
            .HasColumnName("ancho")
            .HasMaxLength(32);
        b.Property(x => x.Largo)
            .HasColumnName("largo")
            .HasMaxLength(32);

        b.Property(x => x.TipoGalpon)
            .HasColumnName("tipo_galpon")
            .HasMaxLength(50);

        // (Opcional) Auditoría si tu entidad hereda de AuditableEntity
        // b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        // b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        // b.Property(x => x.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamp with time zone");
        // b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        // b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");

        // --------- Índices ----------
        // Único por compañía + galpón (si quieres multi-tenant unique)
        b.HasIndex(x => new { x.CompanyId, x.GalponId })
            .HasDatabaseName("ux_galpon_company_galpon")
            .IsUnique();

        b.HasIndex(x => new { x.GranjaId, x.NucleoId })
            .HasDatabaseName("ix_galpon_granja_nucleo");

        b.HasIndex(x => x.GalponNombre)
            .HasDatabaseName("ix_galpon_nombre");

        // --------- Relaciones ----------
        // Farm (simple)
        b.HasOne(x => x.Farm)
         .WithMany(f => f.Galpones)
         .HasForeignKey(x => x.GranjaId)
         .OnDelete(DeleteBehavior.Restrict);

        // Nucleo (compuesta): evita shadow props y asegura matching de tipos con PK de Nucleo
        b.HasOne(x => x.Nucleo)
         .WithMany(n => n.Galpones)
         .HasForeignKey(x => new { x.NucleoId, x.GranjaId })
         .HasPrincipalKey(n => new { n.NucleoId, n.GranjaId })
         .OnDelete(DeleteBehavior.Restrict);

        // Company (si tienes entidad Company)
        b.HasOne(x => x.Company)
         .WithMany() // si existe Company.Galpones: .WithMany(c => c.Galpones)
         .HasForeignKey(x => x.CompanyId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
