using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class TicketResolutorRolConfiguration : IEntityTypeConfiguration<TicketResolutorRol>
{
    public void Configure(EntityTypeBuilder<TicketResolutorRol> b)
    {
        b.ToTable("ticket_resolutor_rol", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.RoleId).HasColumnName("role_id").IsRequired();
        b.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();
        b.Property(x => x.PaisId).HasColumnName("pais_id");           // NULL = global
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc', now())").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(x => new { x.RoleId, x.Tipo, x.PaisId, x.CompanyId })
            .IsUnique().HasDatabaseName("ux_ticket_resolutor_rol_role_tipo_pais_company");
        b.HasIndex(x => x.RoleId).HasDatabaseName("ix_ticket_resolutor_rol_role_id");
        b.HasIndex(x => x.Tipo).HasDatabaseName("ix_ticket_resolutor_rol_tipo");
        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_ticket_resolutor_rol_company_id");
    }
}
