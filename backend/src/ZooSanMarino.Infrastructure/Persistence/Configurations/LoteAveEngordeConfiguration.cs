// src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteAveEngordeConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LoteAveEngordeConfiguration : IEntityTypeConfiguration<LoteAveEngorde>
{
    public void Configure(EntityTypeBuilder<LoteAveEngorde> b)
    {
        b.ToTable("lote_ave_engorde", schema: "public");
        b.HasKey(x => x.LoteAveEngordeId);

        b.Property(x => x.LoteAveEngordeId).HasColumnName("lote_ave_engorde_id").ValueGeneratedOnAdd();
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

        b.HasIndex(x => x.GranjaId).HasDatabaseName("ix_lote_ave_engorde_granja");
        b.HasIndex(x => x.NucleoId).HasDatabaseName("ix_lote_ave_engorde_nucleo");
        b.HasIndex(x => x.GalponId).HasDatabaseName("ix_lote_ave_engorde_galpon");

        b.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_lae_nonneg_counts",
                "(hembras_l >= 0 OR hembras_l IS NULL) AND (machos_l >= 0 OR machos_l IS NULL) AND (mixtas >= 0 OR mixtas IS NULL) AND (aves_encasetadas >= 0 OR aves_encasetadas IS NULL)"
            );
            t.HasCheckConstraint(
                "ck_lae_nonneg_pesos",
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
    }
}
