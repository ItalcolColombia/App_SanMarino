using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class TicketResolutorConfiguration : IEntityTypeConfiguration<TicketResolutor>
{
    public void Configure(EntityTypeBuilder<TicketResolutor> b)
    {
        b.ToTable("ticket_resolutores", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        b.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();
        b.Property(x => x.PaisId).HasColumnName("pais_id");           // NULL = global
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc', now())").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // Unicidad: un usuario no puede tener el mismo tipo+país+empresa dos veces
        b.HasIndex(x => new { x.UserId, x.Tipo, x.PaisId, x.CompanyId })
            .IsUnique().HasDatabaseName("ux_ticket_resolutores_user_tipo_pais_company");

        b.HasIndex(x => x.Tipo).HasDatabaseName("ix_ticket_resolutores_tipo");
        b.HasIndex(x => x.PaisId).HasDatabaseName("ix_ticket_resolutores_pais_id");
        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_ticket_resolutores_company_id");
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_ticket_resolutores_user_id");
    }
}
