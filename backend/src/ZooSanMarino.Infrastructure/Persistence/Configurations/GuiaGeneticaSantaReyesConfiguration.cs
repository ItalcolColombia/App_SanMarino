// src/ZooSanMarino.Infrastructure/Persistence/Configurations/GuiaGeneticaSantaReyesConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class GuiaGeneticaSantaReyesConfiguration : IEntityTypeConfiguration<GuiaGeneticaSantaReyes>
{
    public void Configure(EntityTypeBuilder<GuiaGeneticaSantaReyes> b)
    {
        b.ToTable("guia_genetica_santa_reyes", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        b.Property(x => x.Raza).HasColumnName("raza").HasMaxLength(80).IsRequired();
        b.Property(x => x.AnioGuia).HasColumnName("anio_guia").HasMaxLength(10).IsRequired();
        b.Property(x => x.Edad).HasColumnName("edad").IsRequired();

        b.Property(x => x.ProdPorcentaje).HasColumnName("prod_porcentaje").HasPrecision(6, 2).IsRequired(false);
        b.Property(x => x.RetiroAcH).HasColumnName("retiro_ac_h").HasPrecision(6, 2).IsRequired(false);
        b.Property(x => x.GrAveDiaH).HasColumnName("gr_ave_dia_h").HasPrecision(7, 2).IsRequired(false);

        b.Property(x => x.CodigoGuiaGenetica).HasColumnName("codigo_guia_genetica").HasMaxLength(150).IsRequired(false);

        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired(false);
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired(false);
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at").IsRequired(false);

        b.HasIndex(x => new { x.CompanyId, x.Raza, x.AnioGuia })
            .HasDatabaseName("ix_guia_genetica_santa_reyes_raza_anio");

        // Clave natural para upsert idempotente por seed/import, igual que el patron de
        // ProduccionAvicolaRaw (Raza+AnioGuia+Edad). Filtrada por deleted_at: una linea dada de baja
        // no bloquea recrear el mismo codigo.
        b.HasIndex(x => new { x.CompanyId, x.CodigoGuiaGenetica })
            .IsUnique()
            .HasFilter("deleted_at IS NULL AND codigo_guia_genetica IS NOT NULL")
            .HasDatabaseName("ux_guia_genetica_santa_reyes_codigo");
    }
}
