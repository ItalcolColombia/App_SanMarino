using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class TicketNotaConfiguration : IEntityTypeConfiguration<TicketNota>
{
    public void Configure(EntityTypeBuilder<TicketNota> b)
    {
        b.ToTable("ticket_notas", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.TicketId).HasColumnName("ticket_id").IsRequired();
        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        b.Property(x => x.Nota).HasColumnName("nota").IsRequired();
        b.Property(x => x.EstadoResultante).HasColumnName("estado_resultante").HasMaxLength(20);
        b.Property(x => x.EsInterna)
            .HasColumnName("es_interna")
            .HasDefaultValue(false)
            .IsRequired();
        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();

        b.HasIndex(x => x.TicketId).HasDatabaseName("ix_ticket_notas_ticket_id");
        b.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_ticket_notas_created_at");
    }
}
