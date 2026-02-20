// src/ZooSanMarino.Infrastructure/Persistence/Configurations/MovimientoPolloEngordeConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class MovimientoPolloEngordeConfiguration : IEntityTypeConfiguration<MovimientoPolloEngorde>
{
    public void Configure(EntityTypeBuilder<MovimientoPolloEngorde> b)
    {
        b.ToTable("movimiento_pollo_engorde");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

        b.Property(x => x.NumeroMovimiento).HasColumnName("numero_movimiento").HasMaxLength(50).IsRequired();
        b.Property(x => x.FechaMovimiento).HasColumnName("fecha_movimiento").HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.TipoMovimiento).HasColumnName("tipo_movimiento").HasMaxLength(50).IsRequired();

        b.Property(x => x.LoteAveEngordeOrigenId).HasColumnName("lote_ave_engorde_origen_id");
        b.Property(x => x.LoteReproductoraAveEngordeOrigenId).HasColumnName("lote_reproductora_ave_engorde_origen_id");
        b.Property(x => x.GranjaOrigenId).HasColumnName("granja_origen_id");
        b.Property(x => x.NucleoOrigenId).HasColumnName("nucleo_origen_id").HasMaxLength(64);
        b.Property(x => x.GalponOrigenId).HasColumnName("galpon_origen_id").HasMaxLength(64);

        b.Property(x => x.LoteAveEngordeDestinoId).HasColumnName("lote_ave_engorde_destino_id");
        b.Property(x => x.LoteReproductoraAveEngordeDestinoId).HasColumnName("lote_reproductora_ave_engorde_destino_id");
        b.Property(x => x.GranjaDestinoId).HasColumnName("granja_destino_id");
        b.Property(x => x.NucleoDestinoId).HasColumnName("nucleo_destino_id").HasMaxLength(64);
        b.Property(x => x.GalponDestinoId).HasColumnName("galpon_destino_id").HasMaxLength(64);
        b.Property(x => x.PlantaDestino).HasColumnName("planta_destino").HasMaxLength(200);

        b.Property(x => x.CantidadHembras).HasColumnName("cantidad_hembras").HasDefaultValue(0);
        b.Property(x => x.CantidadMachos).HasColumnName("cantidad_machos").HasDefaultValue(0);
        b.Property(x => x.CantidadMixtas).HasColumnName("cantidad_mixtas").HasDefaultValue(0);

        b.Property(x => x.MotivoMovimiento).HasColumnName("motivo_movimiento").HasMaxLength(500);
        b.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(1000);
        b.Property(x => x.Observaciones).HasColumnName("observaciones").HasMaxLength(1000);
        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).HasDefaultValue("Pendiente");

        b.Property(x => x.UsuarioMovimientoId).HasColumnName("usuario_movimiento_id").IsRequired();
        b.Property(x => x.UsuarioNombre).HasColumnName("usuario_nombre").HasMaxLength(200);
        b.Property(x => x.FechaProcesamiento).HasColumnName("fecha_procesamiento").HasColumnType("timestamp with time zone");
        b.Property(x => x.FechaCancelacion).HasColumnName("fecha_cancelacion").HasColumnType("timestamp with time zone");

        b.Property(x => x.NumeroDespacho).HasColumnName("numero_despacho").HasMaxLength(50);
        b.Property(x => x.EdadAves).HasColumnName("edad_aves");
        b.Property(x => x.TotalPollosGalpon).HasColumnName("total_pollos_galpon");
        b.Property(x => x.Raza).HasColumnName("raza").HasMaxLength(100);
        b.Property(x => x.Placa).HasColumnName("placa").HasMaxLength(20);
        b.Property(x => x.HoraSalida).HasColumnName("hora_salida").HasColumnType("time");
        b.Property(x => x.GuiaAgrocalidad).HasColumnName("guia_agrocalidad").HasMaxLength(100);
        b.Property(x => x.Sellos).HasColumnName("sellos").HasMaxLength(500);
        b.Property(x => x.Ayuno).HasColumnName("ayuno").HasMaxLength(50);
        b.Property(x => x.Conductor).HasColumnName("conductor").HasMaxLength(200);
        b.Property(x => x.PesoBruto).HasColumnName("peso_bruto");
        b.Property(x => x.PesoTara).HasColumnName("peso_tara");

        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamp with time zone");

        b.HasOne(x => x.LoteAveEngordeOrigen)
            .WithMany()
            .HasForeignKey(x => x.LoteAveEngordeOrigenId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LoteReproductoraAveEngordeOrigen)
            .WithMany()
            .HasForeignKey(x => x.LoteReproductoraAveEngordeOrigenId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LoteAveEngordeDestino)
            .WithMany()
            .HasForeignKey(x => x.LoteAveEngordeDestinoId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LoteReproductoraAveEngordeDestino)
            .WithMany()
            .HasForeignKey(x => x.LoteReproductoraAveEngordeDestinoId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.GranjaOrigen)
            .WithMany()
            .HasForeignKey(x => x.GranjaOrigenId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.GranjaDestino)
            .WithMany()
            .HasForeignKey(x => x.GranjaDestinoId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.NumeroMovimiento).IsUnique().HasDatabaseName("uq_movimiento_pollo_engorde_numero");
        b.HasIndex(x => x.FechaMovimiento).HasDatabaseName("ix_movimiento_pollo_engorde_fecha");
        b.HasIndex(x => x.Estado).HasDatabaseName("ix_movimiento_pollo_engorde_estado");
        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_movimiento_pollo_engorde_company_id");
        b.Ignore(x => x.TotalAves);
        b.Ignore(x => x.PesoNeto);
        b.Ignore(x => x.PromedioPesoAve);
    }
}
