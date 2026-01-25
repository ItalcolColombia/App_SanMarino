using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class SeguimientoProduccionConfiguration : IEntityTypeConfiguration<SeguimientoProduccion>
{
    public void Configure(EntityTypeBuilder<SeguimientoProduccion> builder)
    {
        builder.ToTable("produccion_diaria");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .UseIdentityAlwaysColumn()
            .HasColumnName("id");

        // lote_id es text en la BD
        builder.Property(x => x.LoteId)
            .IsRequired()
            .HasColumnName("lote_id")
            .HasColumnType("text");
        
        builder.Property(x => x.Fecha)
            .IsRequired()
            .HasColumnName("fecha_registro")
            .HasColumnType("timestamp with time zone");
        
        builder.Property(x => x.MortalidadH)
            .IsRequired()
            .HasColumnName("mortalidad_hembras");
        
        builder.Property(x => x.MortalidadM)
            .IsRequired()
            .HasColumnName("mortalidad_machos");
        
        builder.Property(x => x.SelH)
            .IsRequired()
            .HasColumnName("sel_h");

        builder.Property(x => x.SelM)
            .IsRequired()
            .HasColumnName("sel_m")
            .HasDefaultValue(0);
        
        builder.Property(x => x.ConsKgH)
            .IsRequired()
            .HasColumnName("cons_kg_h")
            .HasColumnType("double precision");
        
        builder.Property(x => x.ConsKgM)
            .IsRequired()
            .HasColumnName("cons_kg_m")
            .HasColumnType("double precision");

        builder.Property(x => x.HuevoTot)
            .IsRequired()
            .HasColumnName("huevo_tot");
        
        builder.Property(x => x.HuevoInc)
            .IsRequired()
            .HasColumnName("huevo_inc");
        
        // Campos de Clasificadora de Huevos
        // (Limpio, Tratado) = HuevoInc +
        builder.Property(x => x.HuevoLimpio)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevo_limpio");
        
        builder.Property(x => x.HuevoTratado)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevo_tratado");
        
        // (Sucio, Deforme, Blanco, Doble Yema, Piso, Pequeño, Roto, Desecho, Otro) = Huevo Total
        builder.Property(x => x.HuevoSucio)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevo_sucio");
        
        builder.Property(x => x.HuevoDeforme)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevo_deforme");
        
        builder.Property(x => x.HuevoBlanco)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevo_blanco");
        
        builder.Property(x => x.HuevoDobleYema)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevo_doble_yema");
        
        builder.Property(x => x.HuevoPiso)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevo_piso");
        
        builder.Property(x => x.HuevoPequeno)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevo_pequeno");
        
        builder.Property(x => x.HuevoRoto)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevo_roto");
        
        builder.Property(x => x.HuevoDesecho)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevo_desecho");
        
        builder.Property(x => x.HuevoOtro)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("huevo_otro");
        
        builder.Property(x => x.TipoAlimento)
            .IsRequired()
            .HasColumnName("tipo_alimento")
            .HasColumnType("text");
        
        builder.Property(x => x.Observaciones)
            .HasColumnName("observaciones")
            .HasColumnType("text");

        builder.Property(x => x.PesoHuevo)
            .HasColumnName("peso_huevo")
            .HasColumnType("double precision");

        builder.Property(x => x.Etapa)
            .IsRequired()
            .HasColumnName("etapa");

        // Campos de Pesaje Semanal (registro una vez por semana)
        builder.Property(x => x.PesoH)
            .HasColumnName("peso_h")
            .HasColumnType("numeric(8,2)");

        builder.Property(x => x.PesoM)
            .HasColumnName("peso_m")
            .HasColumnType("numeric(8,2)");

        builder.Property(x => x.Uniformidad)
            .HasColumnName("uniformidad")
            .HasColumnType("numeric(5,2)");

        builder.Property(x => x.CoeficienteVariacion)
            .HasColumnName("coeficiente_variacion")
            .HasColumnType("numeric(5,2)");

        builder.Property(x => x.ObservacionesPesaje)
            .HasColumnName("observaciones_pesaje")
            .HasColumnType("text");

        // Metadata JSONB para campos adicionales
        // NOTA: Si la columna no existe en la BD, ejecutar: backend/sql/add_metadata_column_seguimiento_produccion.sql
        builder.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .IsRequired(false);

        // NOTA: No hay relación de navegación con Lote porque:
        // - lote_id en produccion_diaria es text
        // - Lote.LoteId es int?
        // - Son tipos incompatibles para foreign key
        // Si necesitas acceder al Lote, hazlo manualmente usando el string del lote_id

        // Índice único por lote y fecha
        builder.HasIndex(x => new { x.LoteId, x.Fecha }).IsUnique();
    }
}



