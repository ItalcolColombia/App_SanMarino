using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LesionConfiguration : IEntityTypeConfiguration<Lesion>
{
    public void Configure(EntityTypeBuilder<Lesion> b)
    {
        b.ToTable("lesiones", "public");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        b.Property(x => x.ClienteId).HasColumnName("cliente_id");
        b.Property(x => x.FarmId).HasColumnName("farm_id").IsRequired();
        b.Property(x => x.GalponId).HasColumnName("galpon_id").HasMaxLength(50);
        b.Property(x => x.LoteId).HasColumnName("lote_id");
        b.Property(x => x.LoteReproductoraId).HasColumnName("lote_reproductora_id").HasMaxLength(50);

        b.Property(x => x.EdadDias).HasColumnName("edad_dias");
        b.Property(x => x.AvesMacho).HasColumnName("aves_macho");
        b.Property(x => x.AvesHembra).HasColumnName("aves_hembra");
        b.Property(x => x.AvesMixtas).HasColumnName("aves_mixtas");

        b.Property(x => x.TipoLesion)
            .HasColumnName("tipo_lesion")
            .HasMaxLength(120)
            .IsRequired();

        b.Property(x => x.Observaciones)
            .HasColumnName("observaciones");

        b.Property(x => x.FechaRegistro)
            .HasColumnName("fecha_registro")
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();

        b.Property(x => x.ModuloOrigen)
            .HasColumnName("modulo_origen")
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(1)
            .HasDefaultValue("A")
            .IsRequired();

        // Auditoría
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        // Índices para filtros típicos
        b.HasIndex(x => x.FarmId).HasDatabaseName("ix_lesiones_farm_id");
        b.HasIndex(x => x.ClienteId).HasDatabaseName("ix_lesiones_cliente_id");
        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_lesiones_company_id");
        b.HasIndex(x => x.ModuloOrigen).HasDatabaseName("ix_lesiones_modulo_origen");
        b.HasIndex(x => x.LoteId).HasDatabaseName("ix_lesiones_lote_id");
        b.HasIndex(x => x.FechaRegistro).HasDatabaseName("ix_lesiones_fecha_registro");
    }
}
