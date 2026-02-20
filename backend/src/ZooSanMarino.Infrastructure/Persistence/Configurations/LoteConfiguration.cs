// src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LoteConfiguration : IEntityTypeConfiguration<Lote>
{
    public void Configure(EntityTypeBuilder<Lote> b)
    {
        b.ToTable("lotes", schema: "public"); // ← coincide con lo que muestra el log
        b.HasKey(x => x.LoteId);

        b.Property(x => x.LoteId).HasColumnName("lote_id").ValueGeneratedOnAdd(); // Auto-incremento numérico
        b.Property(x => x.LoteNombre).HasColumnName("lote_nombre").HasMaxLength(200).IsRequired();
        b.Property(x => x.GranjaId).HasColumnName("granja_id").IsRequired();
        b.Property(x => x.NucleoId).HasColumnName("nucleo_id").HasMaxLength(64);
        b.Property(x => x.GalponId).HasColumnName("galpon_id").HasMaxLength(64);
        b.Property(x => x.Regional).HasColumnName("regional").HasMaxLength(100);
        b.Property(x => x.FechaEncaset).HasColumnName("fecha_encaset");

        b.Property(x => x.HembrasL).HasColumnName("hembras_l");
        b.Property(x => x.MachosL).HasColumnName("machos_l");

        // ← TIPOS EXACTOS EN BD
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
        b.Property(x => x.EstadoTraslado).HasColumnName("estado_traslado").HasMaxLength(50);
        b.Property(x => x.LotePadreId).HasColumnName("lote_padre_id");
        b.Property(x => x.Fase).HasColumnName("fase").HasMaxLength(20).IsRequired().HasDefaultValue("Levante");
        b.Property(x => x.FechaInicioProduccion).HasColumnName("fecha_inicio_produccion");
        b.Property(x => x.HembrasInicialesProd).HasColumnName("hembras_iniciales_prod");
        b.Property(x => x.MachosInicialesProd).HasColumnName("machos_iniciales_prod");
        b.Property(x => x.HuevosIniciales).HasColumnName("huevos_iniciales");
        b.Property(x => x.TipoNido).HasColumnName("tipo_nido").HasMaxLength(50);
        b.Property(x => x.NucleoP).HasColumnName("nucleo_p").HasMaxLength(100);
        b.Property(x => x.CicloProduccion).HasColumnName("ciclo_produccion").HasMaxLength(50);
        b.Property(x => x.FechaFinProduccion).HasColumnName("fecha_fin_produccion");
        b.Property(x => x.AvesFinHembrasProd).HasColumnName("aves_fin_hembras_prod");
        b.Property(x => x.AvesFinMachosProd).HasColumnName("aves_fin_machos_prod");

        b.Property(x => x.PaisId).HasColumnName("pais_id");
        b.Property(x => x.PaisNombre).HasColumnName("pais_nombre").HasMaxLength(120);
        b.Property(x => x.EmpresaNombre).HasColumnName("empresa_nombre").HasMaxLength(200);

        // Relaciones
        // Nota: La relación con LoteReproductora está comentada debido al desajuste de tipos:
        // - lotes.lote_id es INTEGER
        // - lote_reproductoras.lote_id es CHARACTER VARYING(64)
        // La validación de integridad referencial se maneja manualmente en el servicio.
        // Ignoramos la propiedad de navegación Reproductoras para evitar que EF Core intente crear la relación:
        // b.Ignore(x => x.Reproductoras); // Comentado porque la propiedad ya está comentada en la entidad

        b.HasIndex(x => x.GranjaId).HasDatabaseName("ix_lote_granja");
        b.HasIndex(x => x.NucleoId).HasDatabaseName("ix_lote_nucleo");
        b.HasIndex(x => x.GalponId).HasDatabaseName("ix_lote_galpon");

        b.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_l_nonneg_counts",
                "(hembras_l >= 0 OR hembras_l IS NULL) AND (machos_l >= 0 OR machos_l IS NULL) AND (mixtas >= 0 OR mixtas IS NULL) AND (aves_encasetadas >= 0 OR aves_encasetadas IS NULL)"
            );
            t.HasCheckConstraint(
                "ck_l_nonneg_pesos",
                "(peso_inicial_h >= 0 OR peso_inicial_h IS NULL) AND (peso_inicial_m >= 0 OR peso_inicial_m IS NULL) AND (peso_mixto >= 0 OR peso_mixto IS NULL)"
            );
        });

        // Nucleo (FK compuesta nucleo_id + granja_id)
        b.HasOne(x => x.Nucleo)
         .WithMany(n => n.Lotes)
         .HasForeignKey(x => new { x.NucleoId, x.GranjaId })
         .OnDelete(DeleteBehavior.Restrict);

        // Galpon (FK simple por galpon_id + granja_id opcional según tu modelo)
       b.HasOne(x => x.Galpon)
       .WithMany()                        // ← sin 'g => g.Lotes'
       .HasForeignKey(x => x.GalponId)
       .HasPrincipalKey(g => g.GalponId)  // explícito (opcional, PK por defecto)
       .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Farm)
         .WithMany(f => f.Lotes)
         .HasForeignKey(x => x.GranjaId)
         .OnDelete(DeleteBehavior.Restrict);

        // Relación self-referencial para lote padre
        b.HasOne(x => x.LotePadre)
         .WithMany(x => x.LotesHijos)
         .HasForeignKey(x => x.LotePadreId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.LotePadreId).HasDatabaseName("ix_lote_padre");
    }
}
