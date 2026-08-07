using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class TicketTiempoConfiguration : IEntityTypeConfiguration<TicketTiempo>
{
    public void Configure(EntityTypeBuilder<TicketTiempo> b)
    {
        b.ToTable("ticket_tiempos", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        // Opcional: las horas de una tarea nacida en ItalJira no tienen caso al que imputarse.
        b.Property(x => x.TicketId).HasColumnName("ticket_id");
        b.Property(x => x.TareaId).HasColumnName("tarea_id");
        b.Property(x => x.UserGuid).HasColumnName("user_guid");
        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        b.Property(x => x.Fecha).HasColumnName("fecha").IsRequired();
        b.Property(x => x.Horas).HasColumnName("horas").HasPrecision(6, 2).IsRequired();
        b.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(500);
        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasIndex(x => x.TicketId).HasDatabaseName("ix_ticket_tiempos_ticket_id");
        b.HasIndex(x => x.TareaId).HasDatabaseName("ix_ticket_tiempos_tarea_id");
        b.HasIndex(x => x.UserGuid).HasDatabaseName("ix_ticket_tiempos_user_guid");
    }
}
