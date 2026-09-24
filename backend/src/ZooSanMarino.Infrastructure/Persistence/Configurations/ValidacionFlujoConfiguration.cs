using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ValidacionFlujoConfiguration : IEntityTypeConfiguration<ValidacionFlujo>
{
    public void Configure(EntityTypeBuilder<ValidacionFlujo> e)
    {
        e.ToTable("validacion_flujos", "public", t =>
        {
            t.HasCheckConstraint("ck_validacion_flujos_plazo_positivo", "plazo_total_horas > 0");
            t.HasCheckConstraint("ck_validacion_flujos_estado_valido",
                "estado IN ('BORRADOR','PUBLICADO','RETIRADO')");
        });

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        e.Property(x => x.ProcesoId).HasColumnName("proceso_id").IsRequired();
        e.Property(x => x.Version).HasColumnName("version").IsRequired();
        e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(160).IsRequired();
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(12)
            .HasDefaultValue(EstadoFlujoValidacion.Borrador).IsRequired();
        e.Property(x => x.PlazoTotalHoras).HasColumnName("plazo_total_horas").HasDefaultValue(24).IsRequired();
        e.Property(x => x.RequierePersonasDistintas).HasColumnName("requiere_personas_distintas")
            .HasDefaultValue(true).IsRequired();
        e.Property(x => x.PermiteAprobacionCreador).HasColumnName("permite_aprobacion_creador")
            .HasDefaultValue(false).IsRequired();

        e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        e.Property(x => x.PublishedByUserId).HasColumnName("published_by_user_id");

        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz")
            .HasDefaultValueSql("now()").IsRequired();
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
        e.Property(x => x.PublishedAt).HasColumnName("published_at").HasColumnType("timestamptz");
        e.Property(x => x.RetiredAt).HasColumnName("retired_at").HasColumnType("timestamptz");

        e.HasIndex(x => new { x.CompanyId, x.ProcesoId, x.Version })
            .IsUnique().HasDatabaseName("ux_validacion_flujos_company_proceso_version");

        e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Proceso).WithMany(p => p.Flujos).HasForeignKey(x => x.ProcesoId).OnDelete(DeleteBehavior.Restrict);

        e.HasMany(x => x.Pasos).WithOne(p => p.Flujo).HasForeignKey(p => p.FlujoId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Instancias).WithOne(i => i.Flujo).HasForeignKey(i => i.FlujoId).OnDelete(DeleteBehavior.Restrict);
    }
}
