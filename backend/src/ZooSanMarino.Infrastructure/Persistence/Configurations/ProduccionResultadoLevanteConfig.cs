// file: backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/ProduccionResultadoLevanteConfig.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

public class ProduccionResultadoLevanteConfig : IEntityTypeConfiguration<ProduccionResultadoLevante>
{
    public void Configure(EntityTypeBuilder<ProduccionResultadoLevante> b)
    {
        b.HasNoKey();
        b.ToTable("produccion_resultado_levante");
        b.Property(x => x.LoteId).HasColumnName("lote_id");
        b.Property(x => x.Fecha).HasColumnName("fecha");
        b.Property(x => x.EdadSemana).HasColumnName("edad_semana");

        // H
        b.Property(x => x.HembraViva).HasColumnName("hembra_viva");
        b.Property(x => x.MortH).HasColumnName("mort_h");
        b.Property(x => x.SelHOut).HasColumnName("sel_h_out");
        b.Property(x => x.ErrH).HasColumnName("err_h");
        b.Property(x => x.ConsKgH).HasColumnName("cons_kg_h");
        b.Property(x => x.PesoH).HasColumnName("peso_h");
        b.Property(x => x.UnifH).HasColumnName("unif_h");
        b.Property(x => x.CvH).HasColumnName("cv_h");
        b.Property(x => x.MortHPct).HasColumnName("mort_h_pct");
        b.Property(x => x.SelHPct).HasColumnName("sel_h_pct");
        b.Property(x => x.ErrHPct).HasColumnName("err_h_pct");
        b.Property(x => x.MsEhH).HasColumnName("ms_eh_h");
        b.Property(x => x.AcMortH).HasColumnName("ac_mort_h");
        b.Property(x => x.AcSelH).HasColumnName("ac_sel_h");
        b.Property(x => x.AcErrH).HasColumnName("ac_err_h");
        b.Property(x => x.AcConsKgH).HasColumnName("ac_cons_kg_h");
        b.Property(x => x.ConsAcGrH).HasColumnName("cons_ac_gr_h");
        b.Property(x => x.GrAveDiaH).HasColumnName("gr_ave_dia_h");
        b.Property(x => x.DifConsHPct).HasColumnName("dif_cons_h_pct");
        b.Property(x => x.DifPesoHPct).HasColumnName("dif_peso_h_pct");
        b.Property(x => x.RetiroHPct).HasColumnName("retiro_h_pct");
        b.Property(x => x.RetiroHAcPct).HasColumnName("retiro_h_ac_pct");

        // M
        b.Property(x => x.MachoVivo).HasColumnName("macho_vivo");
        b.Property(x => x.MortM).HasColumnName("mort_m");
        b.Property(x => x.SelMOut).HasColumnName("sel_m_out");
        b.Property(x => x.ErrM).HasColumnName("err_m");
        b.Property(x => x.ConsKgM).HasColumnName("cons_kg_m");
        b.Property(x => x.PesoM).HasColumnName("peso_m");
        b.Property(x => x.UnifM).HasColumnName("unif_m");
        b.Property(x => x.CvM).HasColumnName("cv_m");
        b.Property(x => x.MortMPct).HasColumnName("mort_m_pct");
        b.Property(x => x.SelMPct).HasColumnName("sel_m_pct");
        b.Property(x => x.ErrMPct).HasColumnName("err_m_pct");
        b.Property(x => x.MsEmM).HasColumnName("ms_em_m");
        b.Property(x => x.AcMortM).HasColumnName("ac_mort_m");
        b.Property(x => x.AcSelM).HasColumnName("ac_sel_m");
        b.Property(x => x.AcErrM).HasColumnName("ac_err_m");
        b.Property(x => x.AcConsKgM).HasColumnName("ac_cons_kg_m");
        b.Property(x => x.ConsAcGrM).HasColumnName("cons_ac_gr_m");
        b.Property(x => x.GrAveDiaM).HasColumnName("gr_ave_dia_m");
        b.Property(x => x.DifConsMPct).HasColumnName("dif_cons_m_pct");
        b.Property(x => x.DifPesoMPct).HasColumnName("dif_peso_m_pct");
        b.Property(x => x.RetiroMPct).HasColumnName("retiro_m_pct");
        b.Property(x => x.RetiroMAcPct).HasColumnName("retiro_m_ac_pct");

        // Rel/Guía
        b.Property(x => x.RelMHPct).HasColumnName("rel_m_h_pct");
        b.Property(x => x.PesoHGuia).HasColumnName("peso_h_guia");
        b.Property(x => x.UnifHGuia).HasColumnName("unif_h_guia");
        b.Property(x => x.ConsAcGrHGuia).HasColumnName("cons_ac_gr_h_guia");
        b.Property(x => x.GrAveDiaHGuia).HasColumnName("gr_ave_dia_h_guia");
        b.Property(x => x.MortHPctGuia).HasColumnName("mort_h_pct_guia");
        b.Property(x => x.PesoMGuia).HasColumnName("peso_m_guia");
        b.Property(x => x.UnifMGuia).HasColumnName("unif_m_guia");
        b.Property(x => x.ConsAcGrMGuia).HasColumnName("cons_ac_gr_m_guia");
        b.Property(x => x.GrAveDiaMGuia).HasColumnName("gr_ave_dia_m_guia");
        b.Property(x => x.MortMPctGuia).HasColumnName("mort_m_pct_guia");
        b.Property(x => x.AlimentoHGuia).HasColumnName("alimento_h_guia");
        b.Property(x => x.AlimentoMGuia).HasColumnName("alimento_m_guia");
    }
}
