using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class MapaConfiguration : IEntityTypeConfiguration<Mapa>
{
    public void Configure(EntityTypeBuilder<Mapa> b)
    {
        b.ToTable("mapa");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
        b.Property(x => x.Descripcion).HasColumnName("descripcion").HasColumnType("text");
        b.Property(x => x.CodigoPlantilla).HasColumnName("codigo_plantilla").HasMaxLength(80);
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.PaisId).HasColumnName("pais_id");
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamp with time zone");

        b.HasOne(x => x.Pais).WithMany().HasForeignKey(x => x.PaisId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.Pasos).WithOne(p => p.Mapa).HasForeignKey(p => p.MapaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Ejecuciones).WithOne(e => e.Mapa).HasForeignKey(e => e.MapaId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.CompanyId);
        b.HasIndex(x => x.DeletedAt).HasFilter("deleted_at IS NULL");
    }
}
