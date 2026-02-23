// src/ZooSanMarino.Infrastructure/Persistence/Configurations/LotePosturaLevanteConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LotePosturaLevanteConfiguration : IEntityTypeConfiguration<LotePosturaLevante>
{
    public void Configure(EntityTypeBuilder<LotePosturaLevante> b)
    {
        b.ToTable("lote_postura_levante", schema: "public");
        b.HasKey(x => x.LotePosturaLevanteId);

        b.Property(x => x.LotePosturaLevanteId).HasColumnName("lote_postura_levante_id").ValueGeneratedOnAdd();
        b.Property(x => x.LoteNombre).HasColumnName("lote_nombre").HasMaxLength(200).IsRequired();
        b.Property(x => x.GranjaId).HasColumnName("granja_id").IsRequired();
        b.Property(x => x.NucleoId).HasColumnName("nucleo_id").HasMaxLength(64);
        b.Property(x => x.GalponId).HasColumnName("galpon_id").HasMaxLength(64);
        b.Property(x => x.Regional).HasColumnName("regional").HasMaxLength(100);
        b.Property(x => x.FechaEncaset).HasColumnName("fecha_encaset");

        b.Property(x => x.HembrasL).HasColumnName("hembras_l");
        b.Property(x => x.MachosL).HasColumnName("machos_l");
        b.Property(x => x.PesoInicialH).HasColumnName("peso_inicial_h").HasColumnType("double precision");
        b.Property(x => x.PesoInicialM).HasColumnName("peso_inicial_m").HasColumnType("double precision");
        b.Property(x => x.UnifH).HasColumnName("unif_h").HasColumnType("double precision");
        b.Property(x => x.UnifM).HasColumnName("unif_m").HasColumnType("double precision");

        b.Property(x => x.MortCajaH).HasColumnName("mort_caja_h");
        b.Property(x => x.MortCajaM).HasColumnName("mort_caja_m");
        b.Property(x => x.Raza).HasColumnName("raza").HasMaxLength(80);
        b.Property(x => x.AnoTablaGenetica).HasColumnName("ano_tabla_genetica");
        b.Property(x => x.Linea).HasColumnName("linea").HasMaxLength(80);
        b.Property(x => x.TipoLinea).HasColumnName("tipo_linea").HasMaxLength(80);
        b.Property(x => x.CodigoGuiaGenetica).HasColumnName("codigo_guia_genetica").HasMaxLength(80);
        b.Property(x => x.LineaGeneticaId).HasColumnName("linea_genetica_id");
        b.Property(x => x.Tecnico).HasColumnName("tecnico").HasMaxLength(120);

        b.Property(x => x.Mixtas).HasColumnName("mixtas");
        b.Property(x => x.PesoMixto).HasColumnName("peso_mixto").HasColumnType("double precision");
        b.Property(x => x.AvesEncasetadas).HasColumnName("aves_encasetadas");
        b.Property(x => x.EdadInicial).HasColumnName("edad_inicial");
        b.Property(x => x.LoteErp).HasColumnName("lote_erp").HasMaxLength(80);
        b.Property(x => x.EstadoTraslado).HasColumnName("estado_traslado").HasMaxLength(50);

        b.Property(x => x.PaisId).HasColumnName("pais_id");
        b.Property(x => x.PaisNombre).HasColumnName("pais_nombre").HasMaxLength(120);
        b.Property(x => x.EmpresaNombre).HasColumnName("empresa_nombre").HasMaxLength(200);

        // Campos específicos postura levante
        b.Property(x => x.LoteId).HasColumnName("lote_id");
        b.Property(x => x.LotePadreId).HasColumnName("lote_padre_id");
        b.Property(x => x.LotePosturaLevantePadreId).HasColumnName("lote_postura_levante_padre_id");
        b.Property(x => x.AvesHInicial).HasColumnName("aves_h_inicial");
        b.Property(x => x.AvesMInicial).HasColumnName("aves_m_inicial");
        b.Property(x => x.AvesHActual).HasColumnName("aves_h_actual");
        b.Property(x => x.AvesMActual).HasColumnName("aves_m_actual");
        b.Property(x => x.EmpresaId).HasColumnName("empresa_id");
        b.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(50);
        b.Property(x => x.Etapa).HasColumnName("etapa").HasMaxLength(50);
        b.Property(x => x.Edad).HasColumnName("edad");
        b.Property(x => x.EstadoCierre).HasColumnName("estado_cierre").HasMaxLength(20);

        b.HasIndex(x => x.GranjaId).HasDatabaseName("ix_lote_postura_levante_granja");
        b.HasIndex(x => x.NucleoId).HasDatabaseName("ix_lote_postura_levante_nucleo");
        b.HasIndex(x => x.GalponId).HasDatabaseName("ix_lote_postura_levante_galpon");
        b.HasIndex(x => x.LoteId).HasDatabaseName("ix_lote_postura_levante_lote");
        b.HasIndex(x => x.LotePosturaLevantePadreId).HasDatabaseName("ix_lote_postura_levante_padre");

        b.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_lpl_nonneg_counts",
                "(hembras_l >= 0 OR hembras_l IS NULL) AND (machos_l >= 0 OR machos_l IS NULL) AND (mixtas >= 0 OR mixtas IS NULL) AND (aves_encasetadas >= 0 OR aves_encasetadas IS NULL)"
            );
            t.HasCheckConstraint(
                "ck_lpl_nonneg_pesos",
                "(peso_inicial_h >= 0 OR peso_inicial_h IS NULL) AND (peso_inicial_m >= 0 OR peso_inicial_m IS NULL) AND (peso_mixto >= 0 OR peso_mixto IS NULL)"
            );
        });

        b.HasOne(x => x.Nucleo)
            .WithMany()
            .HasForeignKey(x => new { x.NucleoId, x.GranjaId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Galpon)
            .WithMany()
            .HasForeignKey(x => x.GalponId)
            .HasPrincipalKey(g => g.GalponId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Farm)
            .WithMany()
            .HasForeignKey(x => x.GranjaId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Lote)
            .WithMany()
            .HasForeignKey(x => x.LoteId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.LotePosturaLevantePadre)
            .WithMany()
            .HasForeignKey(x => x.LotePosturaLevantePadreId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
