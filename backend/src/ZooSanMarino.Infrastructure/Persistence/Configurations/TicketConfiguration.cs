using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.ToTable("tickets", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(20);
        b.Property(x => x.PaisId).HasColumnName("pais_id").IsRequired();
        b.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();
        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).IsRequired();
        b.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(160).IsRequired();
        b.Property(x => x.Descripcion).HasColumnName("descripcion").IsRequired();

        b.Property(x => x.AssignedToUserId).HasColumnName("assigned_to_user_id");
        b.Property(x => x.FechaPrimeraApertura).HasColumnName("fecha_primera_apertura");
        b.Property(x => x.FechaSolucion).HasColumnName("fecha_solucion");

        b.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(1)
            .HasDefaultValue("A")
            .IsRequired();

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

        // Relaciones (cascada hacia imágenes y notas)
        b.HasMany(x => x.Imagenes)
            .WithOne(i => i.Ticket!)
            .HasForeignKey(i => i.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Notas)
            .WithOne(n => n.Ticket!)
            .HasForeignKey(n => n.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índices para filtros típicos
        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_tickets_company_id");
        b.HasIndex(x => x.PaisId).HasDatabaseName("ix_tickets_pais_id");
        b.HasIndex(x => x.Estado).HasDatabaseName("ix_tickets_estado");
        b.HasIndex(x => x.Tipo).HasDatabaseName("ix_tickets_tipo");
        b.HasIndex(x => x.CreatedByUserId).HasDatabaseName("ix_tickets_created_by_user_id");
        b.HasIndex(x => x.AssignedToUserId).HasDatabaseName("ix_tickets_assigned_to_user_id");
        b.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_tickets_created_at");
        b.HasIndex(x => x.Codigo).HasDatabaseName("ix_tickets_codigo");
    }
}
