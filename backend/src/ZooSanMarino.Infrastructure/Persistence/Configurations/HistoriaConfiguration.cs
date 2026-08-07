using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de la épica de ItalJira. Las relaciones hacia tareas y casos usan
/// <see cref="DeleteBehavior.SetNull"/>: borrar una historia NO arrastra el trabajo, lo devuelve a
/// la bandeja «sin historia».
/// </summary>
public class HistoriaConfiguration : IEntityTypeConfiguration<Historia>
{
    public void Configure(EntityTypeBuilder<Historia> b)
    {
        b.ToTable("historias", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(40);
        b.Property(x => x.PaisId).HasColumnName("pais_id").IsRequired();

        b.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        b.Property(x => x.Descripcion).HasColumnName("descripcion");

        b.Property(x => x.Estado)
            .HasColumnName("estado").HasMaxLength(20)
            .HasDefaultValue(HistoriaEstados.Backlog).IsRequired();
        b.Property(x => x.Prioridad)
            .HasColumnName("prioridad").HasMaxLength(20)
            .HasDefaultValue(TicketPrioridades.Media).IsRequired();

        b.Property(x => x.ResponsableUserGuid).HasColumnName("responsable_user_guid");
        b.Property(x => x.Orden).HasColumnName("orden").HasDefaultValue(0).IsRequired();

        b.Property(x => x.HorasEstimadas).HasColumnName("horas_estimadas").HasPrecision(8, 2);
        b.Property(x => x.FechaInicioPlan).HasColumnName("fecha_inicio_plan");
        b.Property(x => x.FechaFinPlan).HasColumnName("fecha_fin_plan");
        b.Property(x => x.FechaInicioReal).HasColumnName("fecha_inicio_real");
        b.Property(x => x.FechaFinReal).HasColumnName("fecha_fin_real");
        b.Property(x => x.Etiquetas).HasColumnName("etiquetas").HasMaxLength(300);

        // Auditoría (AuditableEntity)
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasMany(x => x.Tareas)
            .WithOne(t => t.Historia!)
            .HasForeignKey(t => t.HistoriaId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasMany(x => x.Casos)
            .WithOne(t => t.Historia!)
            .HasForeignKey(t => t.HistoriaId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_historias_company_id");
        b.HasIndex(x => x.Estado).HasDatabaseName("ix_historias_estado");
        b.HasIndex(x => x.ResponsableUserGuid).HasDatabaseName("ix_historias_responsable");
        b.HasIndex(x => x.Codigo).HasDatabaseName("ix_historias_codigo");
    }
}
