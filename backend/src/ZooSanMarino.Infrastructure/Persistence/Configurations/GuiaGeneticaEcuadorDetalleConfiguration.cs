using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class GuiaGeneticaEcuadorDetalleConfiguration : IEntityTypeConfiguration<GuiaGeneticaEcuadorDetalle>
{
    public void Configure(EntityTypeBuilder<GuiaGeneticaEcuadorDetalle> e)
    {
        e.ToTable("guia_genetica_ecuador_detalle");

        e.HasKey(x => x.Id);

        e.Property(x => x.GuiaGeneticaEcuadorHeaderId).HasColumnName("guia_genetica_ecuador_header_id").IsRequired();
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

        e.HasIndex(x => new { x.GuiaGeneticaEcuadorHeaderId, x.Sexo, x.Dia }).IsUnique();

        e.HasOne(x => x.GuiaGeneticaEcuadorHeader)
            .WithMany(h => h.Detalles)
            .HasForeignKey(x => x.GuiaGeneticaEcuadorHeaderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

