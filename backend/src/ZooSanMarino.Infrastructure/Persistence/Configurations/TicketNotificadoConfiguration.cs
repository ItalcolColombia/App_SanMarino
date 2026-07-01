using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class TicketNotificadoConfiguration : IEntityTypeConfiguration<TicketNotificado>
{
    public void Configure(EntityTypeBuilder<TicketNotificado> b)
    {
        b.ToTable("ticket_notificados", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.TicketId).HasColumnName("ticket_id").IsRequired();
        b.Property(x => x.UserGuid).HasColumnName("user_guid");
        b.Property(x => x.Cedula).HasColumnName("cedula").HasMaxLength(20);
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        b.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(255);
        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();

        b.HasOne(x => x.Ticket)
            .WithMany(t => t.Notificados)
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.TicketId).HasDatabaseName("ix_ticket_notificados_ticket_id");
    }
}
