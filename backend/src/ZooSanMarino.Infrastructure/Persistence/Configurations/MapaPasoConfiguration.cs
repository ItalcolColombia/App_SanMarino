using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class MapaPasoConfiguration : IEntityTypeConfiguration<MapaPaso>
{
    public void Configure(EntityTypeBuilder<MapaPaso> b)
    {
        b.ToTable("mapa_paso");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.MapaId).HasColumnName("mapa_id").IsRequired();
        b.Property(x => x.Orden).HasColumnName("orden").HasDefaultValue(1);
        b.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(30).IsRequired();
        b.Property(x => x.NombreEtiqueta).HasColumnName("nombre_etiqueta").HasMaxLength(100);
        b.Property(x => x.ScriptSql).HasColumnName("script_sql").HasColumnType("text");
        b.Property(x => x.Opciones).HasColumnName("opciones").HasColumnType("jsonb");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

        b.HasIndex(x => x.MapaId);
    }
}
