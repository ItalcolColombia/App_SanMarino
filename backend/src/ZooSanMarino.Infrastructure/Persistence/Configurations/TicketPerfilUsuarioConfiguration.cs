using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class TicketPerfilUsuarioConfiguration : IEntityTypeConfiguration<TicketPerfilUsuario>
{
    public void Configure(EntityTypeBuilder<TicketPerfilUsuario> b)
    {
        b.ToTable("ticket_perfil_usuario", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.Nivel).HasColumnName("nivel").HasMaxLength(20).IsRequired();
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc', now())").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // Un usuario tiene un nivel por empresa
        b.HasIndex(x => new { x.UserId, x.CompanyId })
            .IsUnique().HasDatabaseName("ux_ticket_perfil_usuario_user_company");
        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_ticket_perfil_usuario_company_id");
    }
}
