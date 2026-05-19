using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("clientes", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        b.Property(x => x.TipoDocumento)
            .HasColumnName("tipo_documento")
            .HasMaxLength(50)
            .IsRequired();

        b.Property(x => x.NumeroIdentificacion)
            .HasColumnName("numero_identificacion")
            .HasMaxLength(100)
            .IsRequired();

        b.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(200)
            .IsRequired();

        b.Property(x => x.Correo)
            .HasColumnName("correo")
            .HasMaxLength(200);

        b.Property(x => x.Telefono)
            .HasColumnName("telefono")
            .HasMaxLength(50);

        b.Property(x => x.TipoCliente)
            .HasColumnName("tipo_cliente")
            .HasMaxLength(50);

        b.Property(x => x.Pais)
            .HasColumnName("pais")
            .HasMaxLength(100);

        b.Property(x => x.Provincia)
            .HasColumnName("provincia")
            .HasMaxLength(100);

        b.Property(x => x.Distrito)
            .HasColumnName("distrito")
            .HasMaxLength(100);

        b.Property(x => x.Planta)
            .HasColumnName("planta")
            .HasMaxLength(100);

        b.Property(x => x.Zona)
            .HasColumnName("zona")
            .HasMaxLength(100);

        b.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(1)
            .HasDefaultValue("A")
            .IsRequired();

        b.Property(x => x.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        b.Property(x => x.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();

        b.Property(x => x.UpdatedByUserId)
            .HasColumnName("updated_by_user_id");

        b.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        b.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        b.HasIndex(x => new { x.CompanyId, x.NumeroIdentificacion })
            .HasDatabaseName("ux_clientes_company_nro_identificacion")
            .IsUnique();

        b.HasIndex(x => new { x.CompanyId, x.Status })
            .HasDatabaseName("ix_clientes_company_status");
    }
}
