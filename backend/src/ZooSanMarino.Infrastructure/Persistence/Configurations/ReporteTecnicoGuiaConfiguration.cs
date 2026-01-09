// src/ZooSanMarino.Infrastructure/Persistence/Configurations/ReporteTecnicoGuiaConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ReporteTecnicoGuiaConfiguration : IEntityTypeConfiguration<ReporteTecnicoGuia>
{
    public void Configure(EntityTypeBuilder<ReporteTecnicoGuia> builder)
    {
        builder.ToTable("reporte_tecnico_guia", schema: "public");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        builder.Property(x => x.LoteId).HasColumnName("lote_id").IsRequired();
        builder.Property(x => x.Semana).HasColumnName("semana").IsRequired();

        // Valores guía hembras
        builder.Property(x => x.PorcMortHGUIA).HasColumnName("porc_mort_h_guia").HasPrecision(8, 3);
        builder.Property(x => x.RetiroHGUIA).HasColumnName("retiro_h_guia").HasPrecision(8, 3);
        builder.Property(x => x.ConsAcGrHGUIA).HasColumnName("cons_ac_gr_h_guia").HasPrecision(10, 2);
        builder.Property(x => x.GrAveDiaGUIAH).HasColumnName("gr_ave_dia_guia_h").HasPrecision(8, 2);
        builder.Property(x => x.IncrConsHGUIA).HasColumnName("incr_cons_h_guia").HasPrecision(8, 2);
        builder.Property(x => x.PesoHGUIA).HasColumnName("peso_h_guia").HasPrecision(8, 2);
        builder.Property(x => x.UnifHGUIA).HasColumnName("unif_h_guia").HasPrecision(5, 2);

        // Valores guía machos
        builder.Property(x => x.PorcMortMGUIA).HasColumnName("porc_mort_m_guia").HasPrecision(8, 3);
        builder.Property(x => x.RetiroMGUIA).HasColumnName("retiro_m_guia").HasPrecision(8, 3);
        builder.Property(x => x.ConsAcGrMGUIA).HasColumnName("cons_ac_gr_m_guia").HasPrecision(10, 2);
        builder.Property(x => x.GrAveDiaMGUIA).HasColumnName("gr_ave_dia_guia_m").HasPrecision(8, 2);
        builder.Property(x => x.IncrConsMGUIA).HasColumnName("incr_cons_m_guia").HasPrecision(8, 2);
        builder.Property(x => x.PesoMGUIA).HasColumnName("peso_m_guia").HasPrecision(8, 2);
        builder.Property(x => x.UnifMGUIA).HasColumnName("unif_m_guia").HasPrecision(5, 2);

        // Valores nutricionales
        builder.Property(x => x.AlimHGUIA).HasColumnName("alim_h_guia").HasMaxLength(100);
        builder.Property(x => x.KcalSemHGUIA).HasColumnName("kcal_sem_h_guia").HasPrecision(12, 3);
        builder.Property(x => x.ProtSemHGUIA).HasColumnName("prot_sem_h_guia").HasPrecision(8, 3);
        builder.Property(x => x.AlimMGUIA).HasColumnName("alim_m_guia").HasMaxLength(100);
        builder.Property(x => x.KcalSemMGUIA).HasColumnName("kcal_sem_m_guia").HasPrecision(12, 3);
        builder.Property(x => x.ProtSemMGUIA).HasColumnName("prot_sem_m_guia").HasPrecision(8, 3);

        // Datos manuales
        builder.Property(x => x.CodGuia).HasColumnName("cod_guia").HasMaxLength(50);
        builder.Property(x => x.IdLoteRAP).HasColumnName("id_lote_rap").HasMaxLength(50);
        builder.Property(x => x.NucleoL).HasColumnName("nucleo_l").HasMaxLength(50);
        builder.Property(x => x.Traslado).HasColumnName("traslado");
        builder.Property(x => x.Anon).HasColumnName("anon");
        builder.Property(x => x.ErrSexAcH).HasColumnName("err_sex_ac_h");
        builder.Property(x => x.ErrSexAcM).HasColumnName("err_sex_ac_m");

        // Relación con Lote
        builder.HasOne(x => x.Lote)
            .WithMany()
            .HasForeignKey(x => x.LoteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice único para lote + semana
        builder.HasIndex(x => new { x.LoteId, x.Semana })
            .IsUnique()
            .HasDatabaseName("ix_reporte_tecnico_guia_lote_semana");
    }
}

