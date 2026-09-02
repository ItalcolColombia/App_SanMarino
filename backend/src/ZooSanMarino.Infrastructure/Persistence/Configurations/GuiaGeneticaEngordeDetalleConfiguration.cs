using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class GuiaGeneticaEngordeDetalleConfiguration : IEntityTypeConfiguration<GuiaGeneticaEngordeDetalle>
{
    public void Configure(EntityTypeBuilder<GuiaGeneticaEngordeDetalle> e)
    {
        e.ToTable("guia_genetica_detalle");

        e.HasKey(x => x.Id).HasName("guia_genetica_detalle_pkey");

        e.Property(x => x.GuiaGeneticaEngordeHeaderId).HasColumnName("guia_genetica_header_id").IsRequired();
        e.Property(x => x.Sexo).HasColumnName("sexo").HasMaxLength(20).IsRequired();
        e.Property(x => x.Dia).HasColumnName("dia").IsRequired();

        e.Property(x => x.PesoCorporalG).HasColumnName("peso_corporal_g").HasPrecision(18, 3).IsRequired();
        e.Property(x => x.GananciaDiariaG).HasColumnName("ganancia_diaria_g").HasPrecision(18, 3).IsRequired();
        e.Property(x => x.PromedioGananciaDiariaG).HasColumnName("promedio_ganancia_diaria_g").HasPrecision(18, 3).IsRequired();
        e.Property(x => x.CantidadAlimentoDiarioG).HasColumnName("cantidad_alimento_diario_g").HasPrecision(18, 3).IsRequired();
        e.Property(x => x.AlimentoAcumuladoG).HasColumnName("alimento_acumulado_g").HasPrecision(18, 3).IsRequired();

        e.Property(x => x.CA).HasColumnName("ca").HasPrecision(18, 6).IsRequired();
        e.Property(x => x.MortalidadSeleccionDiaria).HasColumnName("mortalidad_seleccion_diaria").HasPrecision(18, 6).IsRequired();

        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        e.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired(false);
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired(false);
        e.Property(x => x.DeletedAt).HasColumnName("deleted_at").IsRequired(false);

        e.HasIndex(x => new { x.GuiaGeneticaEngordeHeaderId, x.Sexo, x.Dia })
            .HasDatabaseName("uq_gge_det_header_sexo_dia").IsUnique();

        e.HasOne(x => x.GuiaGeneticaEngordeHeader)
            .WithMany(h => h.Detalles)
            .HasForeignKey(x => x.GuiaGeneticaEngordeHeaderId)
            // El nombre va FIJO porque la tabla no la creó EF sino `create_guia_genetica_ecuador_tables.sql`,
            // y ahí la constraint quedó como `fk_gge_det_header` — corto, no el largo que EF deriva de los
            // nombres de las entidades. Sin fijarlo, EF creía que se llamaba
            // `fk_guia_genetica_detalle_guia_genetica_header_` y cualquier migración que
            // generara intentaría dropear una constraint que en la base NO EXISTE. Además, atarlo al nombre
            // derivado hacía que renombrar la ENTIDAD moviera la constraint de la BD: un rename de C# no
            // tiene por qué generar DDL.
            .HasConstraintName("fk_gge_det_header")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

