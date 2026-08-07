using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class TicketTareaConfiguration : IEntityTypeConfiguration<TicketTarea>
{
    public void Configure(EntityTypeBuilder<TicketTarea> b)
    {
        b.ToTable("ticket_tareas", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        // Opcional desde ItalJira: una tarea puede nacer en desarrollo, sin caso.
        b.Property(x => x.TicketId).HasColumnName("ticket_id");
        b.Property(x => x.HistoriaId).HasColumnName("historia_id");
        b.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(40);

        b.Property(x => x.Tipo)
            .HasColumnName("tipo").HasMaxLength(20)
            .HasDefaultValue(TicketTareaTipos.Tarea).IsRequired();
        b.Property(x => x.Estado)
            .HasColumnName("estado").HasMaxLength(20)
            .HasDefaultValue(TicketTareaEstados.Backlog).IsRequired();
        b.Property(x => x.Prioridad)
            .HasColumnName("prioridad").HasMaxLength(20)
            .HasDefaultValue(TicketPrioridades.Media).IsRequired();

        b.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        b.Property(x => x.Descripcion).HasColumnName("descripcion");

        b.Property(x => x.AsignadoUserGuid).HasColumnName("asignado_user_guid");
        b.Property(x => x.ParentTareaId).HasColumnName("parent_tarea_id");
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

        // Subtareas: auto-referencia. Restrict para que borrar una padre no arrastre en cascada
        // (el borrado del módulo es lógico; la cascada real solo baja desde el ticket).
        b.HasMany<TicketTarea>()
            .WithOne()
            .HasForeignKey(x => x.ParentTareaId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Tiempos)
            .WithOne(t => t.Tarea!)
            .HasForeignKey(t => t.TareaId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.TicketId).HasDatabaseName("ix_ticket_tareas_ticket_id");
        b.HasIndex(x => x.HistoriaId).HasDatabaseName("ix_ticket_tareas_historia_id");
        b.HasIndex(x => x.Estado).HasDatabaseName("ix_ticket_tareas_estado");
        b.HasIndex(x => x.AsignadoUserGuid).HasDatabaseName("ix_ticket_tareas_asignado");
        b.HasIndex(x => x.ParentTareaId).HasDatabaseName("ix_ticket_tareas_parent");
    }
}
