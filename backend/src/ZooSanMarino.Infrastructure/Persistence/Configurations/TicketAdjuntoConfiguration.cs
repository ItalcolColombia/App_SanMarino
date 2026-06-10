using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class TicketAdjuntoConfiguration : IEntityTypeConfiguration<TicketAdjunto>
{
    public void Configure(EntityTypeBuilder<TicketAdjunto> b)
    {
        b.ToTable("ticket_adjuntos", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.TicketId).HasColumnName("ticket_id").IsRequired();
        b.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();

        b.Property(x => x.ContenidoBase64).HasColumnName("contenido_base64");
        b.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255);
        b.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(120);
        b.Property(x => x.SizeBytes).HasColumnName("size_bytes");

        b.Property(x => x.Url).HasColumnName("url").HasMaxLength(1000);
        b.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(255);

        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();

        b.HasIndex(x => x.TicketId).HasDatabaseName("ix_ticket_adjuntos_ticket_id");
    }
}
