// src/ZooSanMarino.Infrastructure/Persistence/Configurations/SeguimientoDiarioAvesEngordeConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class SeguimientoDiarioAvesEngordeConfiguration : IEntityTypeConfiguration<SeguimientoDiarioAvesEngorde>
{
    public void Configure(EntityTypeBuilder<SeguimientoDiarioAvesEngorde> b)
    {
        b.ToTable("seguimiento_diario_aves_engorde");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.LoteAveEngordeId).HasColumnName("lote_ave_engorde_id").IsRequired();
        b.Property(x => x.Fecha).HasColumnName("fecha").IsRequired();

        b.Property(x => x.MortalidadHembras).HasColumnName("mortalidad_hembras");
        b.Property(x => x.MortalidadMachos).HasColumnName("mortalidad_machos");
        b.Property(x => x.SelH).HasColumnName("sel_h");
        b.Property(x => x.SelM).HasColumnName("sel_m");
        b.Property(x => x.ErrorSexajeHembras).HasColumnName("error_sexaje_hembras");
        b.Property(x => x.ErrorSexajeMachos).HasColumnName("error_sexaje_machos");
        b.Property(x => x.ConsumoKgHembras).HasColumnName("consumo_kg_hembras").HasPrecision(12, 3);
        b.Property(x => x.ConsumoKgMachos).HasColumnName("consumo_kg_machos").HasPrecision(12, 3);
        b.Property(x => x.TipoAlimento).HasColumnName("tipo_alimento").HasMaxLength(100);
        b.Property(x => x.Observaciones).HasColumnName("observaciones");
        b.Property(x => x.Ciclo).HasColumnName("ciclo").HasMaxLength(50);

        b.Property(x => x.PesoPromHembras).HasColumnName("peso_prom_hembras");
        b.Property(x => x.PesoPromMachos).HasColumnName("peso_prom_machos");
        b.Property(x => x.UniformidadHembras).HasColumnName("uniformidad_hembras");
        b.Property(x => x.UniformidadMachos).HasColumnName("uniformidad_machos");
        b.Property(x => x.CvHembras).HasColumnName("cv_hembras");
        b.Property(x => x.CvMachos).HasColumnName("cv_machos");

        b.Property(x => x.ConsumoAguaDiario).HasColumnName("consumo_agua_diario");
        b.Property(x => x.ConsumoAguaPh).HasColumnName("consumo_agua_ph");
        b.Property(x => x.ConsumoAguaOrp).HasColumnName("consumo_agua_orp");
        b.Property(x => x.ConsumoAguaTemperatura).HasColumnName("consumo_agua_temperatura");

        b.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        b.Property(x => x.ItemsAdicionales).HasColumnName("items_adicionales").HasColumnType("jsonb");

        b.Property(x => x.KcalAlH).HasColumnName("kcal_al_h");
        b.Property(x => x.ProtAlH).HasColumnName("prot_al_h");
        b.Property(x => x.KcalAveH).HasColumnName("kcal_ave_h");
        b.Property(x => x.ProtAveH).HasColumnName("prot_ave_h");

        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(64);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.Property(x => x.SaldoAlimentoKg).HasColumnName("saldo_alimento_kg").HasPrecision(18, 3);

        b.HasIndex(x => x.LoteAveEngordeId).HasDatabaseName("ix_seguimiento_diario_aves_engorde_lote");
        b.HasIndex(x => x.Fecha).HasDatabaseName("ix_seguimiento_diario_aves_engorde_fecha");

        b.HasOne(x => x.LoteAveEngorde)
            .WithMany()
            .HasForeignKey(x => x.LoteAveEngordeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
