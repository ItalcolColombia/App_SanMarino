using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class MapaEjecucionConfiguration : IEntityTypeConfiguration<MapaEjecucion>
{
    public void Configure(EntityTypeBuilder<MapaEjecucion> b)
    {
        b.ToTable("mapa_ejecucion");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.MapaId).HasColumnName("mapa_id").IsRequired();
        b.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.Parametros).HasColumnName("parametros").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.TipoArchivo).HasColumnName("tipo_archivo").HasMaxLength(10);
        b.Property(x => x.ResultadoJson).HasColumnName("resultado_json").HasColumnType("jsonb");
        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).IsRequired();
        b.Property(x => x.MensajeError).HasColumnName("mensaje_error").HasColumnType("text");
        b.Property(x => x.MensajeEstado).HasColumnName("mensaje_estado").HasMaxLength(200);
        b.Property(x => x.PasoActual).HasColumnName("paso_actual");
        b.Property(x => x.TotalPasos).HasColumnName("total_pasos");
        b.Property(x => x.FechaEjecucion).HasColumnName("fecha_ejecucion").HasColumnType("timestamp with time zone").IsRequired();

        b.HasOne(x => x.Mapa).WithMany(m => m.Ejecuciones).HasForeignKey(x => x.MapaId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.MapaId);
        b.HasIndex(x => x.FechaEjecucion);
    }
}
