// src/ZooSanMarino.Infrastructure/Persistence/Configurations/TrasladoHuevosConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class TrasladoHuevosConfiguration : IEntityTypeConfiguration<TrasladoHuevos>
{
    public void Configure(EntityTypeBuilder<TrasladoHuevos> builder)
    {
        builder.ToTable("traslado_huevos");
        
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("nextval('traslado_huevos_id_seq')");
        
        builder.Property(t => t.NumeroTraslado)
            .HasColumnName("numero_traslado")
            .HasMaxLength(50)
            .IsRequired();
        
        builder.Property(t => t.FechaTraslado)
            .HasColumnName("fecha_traslado")
            .IsRequired();
        
        builder.Property(t => t.TipoOperacion)
            .HasColumnName("tipo_operacion")
            .HasMaxLength(20)
            .IsRequired();
        
        builder.Property(t => t.LoteId)
            .HasColumnName("lote_id")
            .HasMaxLength(50)
            .IsRequired();
        
        builder.Property(t => t.GranjaOrigenId)
            .HasColumnName("granja_origen_id")
            .IsRequired();
        
        builder.Property(t => t.GranjaDestinoId)
            .HasColumnName("granja_destino_id");
        
        builder.Property(t => t.LoteDestinoId)
            .HasColumnName("lote_destino_id")
            .HasMaxLength(50);
        
        builder.Property(t => t.TipoDestino)
            .HasColumnName("tipo_destino")
            .HasMaxLength(20);
        
        builder.Property(t => t.Motivo)
            .HasColumnName("motivo")
            .HasMaxLength(200);
        
        builder.Property(t => t.Descripcion)
            .HasColumnName("descripcion")
            .HasColumnType("text");
        
        builder.Property(t => t.CantidadLimpio)
            .HasColumnName("cantidad_limpio")
            .HasDefaultValue(0);
        
        builder.Property(t => t.CantidadTratado)
            .HasColumnName("cantidad_tratado")
            .HasDefaultValue(0);
        
        builder.Property(t => t.CantidadSucio)
            .HasColumnName("cantidad_sucio")
            .HasDefaultValue(0);
        
        builder.Property(t => t.CantidadDeforme)
            .HasColumnName("cantidad_deforme")
            .HasDefaultValue(0);
        
        builder.Property(t => t.CantidadBlanco)
            .HasColumnName("cantidad_blanco")
            .HasDefaultValue(0);
        
        builder.Property(t => t.CantidadDobleYema)
            .HasColumnName("cantidad_doble_yema")
            .HasDefaultValue(0);
        
        builder.Property(t => t.CantidadPiso)
            .HasColumnName("cantidad_piso")
            .HasDefaultValue(0);
        
        builder.Property(t => t.CantidadPequeno)
            .HasColumnName("cantidad_pequeno")
            .HasDefaultValue(0);
        
        builder.Property(t => t.CantidadRoto)
            .HasColumnName("cantidad_roto")
            .HasDefaultValue(0);
        
        builder.Property(t => t.CantidadDesecho)
            .HasColumnName("cantidad_desecho")
            .HasDefaultValue(0);
        
        builder.Property(t => t.CantidadOtro)
            .HasColumnName("cantidad_otro")
            .HasDefaultValue(0);
        
        builder.Property(t => t.Estado)
            .HasColumnName("estado")
            .HasMaxLength(20)
            .HasDefaultValue("Pendiente");
        
        builder.Property(t => t.UsuarioTrasladoId)
            .HasColumnName("usuario_traslado_id")
            .IsRequired();
        
        builder.Property(t => t.UsuarioNombre)
            .HasColumnName("usuario_nombre")
            .HasMaxLength(200);
        
        builder.Property(t => t.FechaProcesamiento)
            .HasColumnName("fecha_procesamiento");
        
        builder.Property(t => t.FechaCancelacion)
            .HasColumnName("fecha_cancelacion");
        
        builder.Property(t => t.Observaciones)
            .HasColumnName("observaciones")
            .HasColumnType("text");
        
        // Propiedades de auditoría (heredadas de AuditableEntity)
        builder.Property(t => t.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        builder.Property(t => t.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(t => t.UpdatedByUserId)
            .HasColumnName("updated_by_user_id");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");

        // Índices
        builder.HasIndex(t => t.LoteId);
        builder.HasIndex(t => t.FechaTraslado);
        builder.HasIndex(t => t.Estado);
        builder.HasIndex(t => t.NumeroTraslado)
            .IsUnique();
        builder.HasIndex(t => t.CompanyId);
    }
}

