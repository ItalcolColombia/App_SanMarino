// src/ZooSanMarino.Infrastructure/Persistence/Configurations/SeguimientoDiarioConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class SeguimientoDiarioConfiguration : IEntityTypeConfiguration<SeguimientoDiario>
{
    public void Configure(EntityTypeBuilder<SeguimientoDiario> b)
    {
        b.ToTable("seguimiento_diario", "public");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.TipoSeguimiento).HasColumnName("tipo_seguimiento").HasMaxLength(20).IsRequired();
        b.Property(x => x.LoteId).HasColumnName("lote_id").HasMaxLength(64).IsRequired();
        b.Property(x => x.ReproductoraId).HasColumnName("reproductora_id").HasMaxLength(64);
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

        b.Property(x => x.PesoPromHembras).HasColumnName("peso_prom_hembras").HasColumnType("double precision");
        b.Property(x => x.PesoPromMachos).HasColumnName("peso_prom_machos").HasColumnType("double precision");
        b.Property(x => x.UniformidadHembras).HasColumnName("uniformidad_hembras").HasColumnType("double precision");
        b.Property(x => x.UniformidadMachos).HasColumnName("uniformidad_machos").HasColumnType("double precision");
        b.Property(x => x.CvHembras).HasColumnName("cv_hembras").HasColumnType("double precision");
        b.Property(x => x.CvMachos).HasColumnName("cv_machos").HasColumnType("double precision");

        b.Property(x => x.ConsumoAguaDiario).HasColumnName("consumo_agua_diario").HasColumnType("double precision");
        b.Property(x => x.ConsumoAguaPh).HasColumnName("consumo_agua_ph").HasColumnType("double precision");
        b.Property(x => x.ConsumoAguaOrp).HasColumnName("consumo_agua_orp").HasColumnType("double precision");
        b.Property(x => x.ConsumoAguaTemperatura).HasColumnName("consumo_agua_temperatura").HasColumnType("double precision");

        b.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        b.Property(x => x.ItemsAdicionales).HasColumnName("items_adicionales").HasColumnType("jsonb");

        b.Property(x => x.PesoInicial).HasColumnName("peso_inicial").HasPrecision(10, 3);
        b.Property(x => x.PesoFinal).HasColumnName("peso_final").HasPrecision(10, 3);

        b.Property(x => x.KcalAlH).HasColumnName("kcal_al_h").HasColumnType("double precision");
        b.Property(x => x.ProtAlH).HasColumnName("prot_al_h").HasColumnType("double precision");
        b.Property(x => x.KcalAveH).HasColumnName("kcal_ave_h").HasColumnType("double precision");
        b.Property(x => x.ProtAveH).HasColumnName("prot_ave_h").HasColumnType("double precision");

        b.Property(x => x.HuevoTot).HasColumnName("huevo_tot");
        b.Property(x => x.HuevoInc).HasColumnName("huevo_inc");
        b.Property(x => x.HuevoLimpio).HasColumnName("huevo_limpio");
        b.Property(x => x.HuevoTratado).HasColumnName("huevo_tratado");
        b.Property(x => x.HuevoSucio).HasColumnName("huevo_sucio");
        b.Property(x => x.HuevoDeforme).HasColumnName("huevo_deforme");
        b.Property(x => x.HuevoBlanco).HasColumnName("huevo_blanco");
        b.Property(x => x.HuevoDobleYema).HasColumnName("huevo_doble_yema");
        b.Property(x => x.HuevoPiso).HasColumnName("huevo_piso");
        b.Property(x => x.HuevoPequeno).HasColumnName("huevo_pequeno");
        b.Property(x => x.HuevoRoto).HasColumnName("huevo_roto");
        b.Property(x => x.HuevoDesecho).HasColumnName("huevo_desecho");
        b.Property(x => x.HuevoOtro).HasColumnName("huevo_otro");
        b.Property(x => x.PesoHuevo).HasColumnName("peso_huevo").HasColumnType("double precision");
        b.Property(x => x.Etapa).HasColumnName("etapa");
        b.Property(x => x.PesoH).HasColumnName("peso_h").HasPrecision(8, 2);
        b.Property(x => x.PesoM).HasColumnName("peso_m").HasPrecision(8, 2);
        b.Property(x => x.Uniformidad).HasColumnName("uniformidad").HasPrecision(5, 2);
        b.Property(x => x.CoeficienteVariacion).HasColumnName("coeficiente_variacion").HasPrecision(5, 2);
        b.Property(x => x.ObservacionesPesaje).HasColumnName("observaciones_pesaje");

        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(64);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
