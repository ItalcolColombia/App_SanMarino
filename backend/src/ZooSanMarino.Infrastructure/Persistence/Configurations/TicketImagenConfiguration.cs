using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class TicketImagenConfiguration : IEntityTypeConfiguration<TicketImagen>
{
    public void Configure(EntityTypeBuilder<TicketImagen> b)
    {
        b.ToTable("ticket_imagenes", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.TicketId).HasColumnName("ticket_id").IsRequired();
        b.Property(x => x.ImagenBase64).HasColumnName("imagen_base64").IsRequired();
        b.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(200);
        b.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(60);
        b.Property(x => x.SizeBytes).HasColumnName("size_bytes");
        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();

        b.HasIndex(x => x.TicketId).HasDatabaseName("ix_ticket_imagenes_ticket_id");
    }
}
