// src/ZooSanMarino.Infrastructure/Persistence/Configurations/EspejoHuevoProduccionConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class EspejoHuevoProduccionConfiguration : IEntityTypeConfiguration<EspejoHuevoProduccion>
{
    public void Configure(EntityTypeBuilder<EspejoHuevoProduccion> b)
    {
        b.ToTable("espejo_huevo_produccion");
        b.HasKey(x => x.LotePosturaProduccionId);

        b.Property(x => x.LotePosturaProduccionId).HasColumnName("lote_postura_produccion_id");
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        b.Property(x => x.HuevoTotHistorico).HasColumnName("huevo_tot_historico").HasDefaultValue(0);
        b.Property(x => x.HuevoTotDinamico).HasColumnName("huevo_tot_dinamico").HasDefaultValue(0);
        b.Property(x => x.HuevoIncHistorico).HasColumnName("huevo_inc_historico").HasDefaultValue(0);
        b.Property(x => x.HuevoIncDinamico).HasColumnName("huevo_inc_dinamico").HasDefaultValue(0);
        b.Property(x => x.HuevoLimpioHistorico).HasColumnName("huevo_limpio_historico").HasDefaultValue(0);
        b.Property(x => x.HuevoLimpioDinamico).HasColumnName("huevo_limpio_dinamico").HasDefaultValue(0);
        b.Property(x => x.HuevoTratadoHistorico).HasColumnName("huevo_tratado_historico").HasDefaultValue(0);
        b.Property(x => x.HuevoTratadoDinamico).HasColumnName("huevo_tratado_dinamico").HasDefaultValue(0);
        b.Property(x => x.HuevoSucioHistorico).HasColumnName("huevo_sucio_historico").HasDefaultValue(0);
        b.Property(x => x.HuevoSucioDinamico).HasColumnName("huevo_sucio_dinamico").HasDefaultValue(0);
        b.Property(x => x.HuevoDeformeHistorico).HasColumnName("huevo_deforme_historico").HasDefaultValue(0);
        b.Property(x => x.HuevoDeformeDinamico).HasColumnName("huevo_deforme_dinamico").HasDefaultValue(0);
        b.Property(x => x.HuevoBlancoHistorico).HasColumnName("huevo_blanco_historico").HasDefaultValue(0);
        b.Property(x => x.HuevoBlancoDinamico).HasColumnName("huevo_blanco_dinamico").HasDefaultValue(0);
        b.Property(x => x.HuevoDobleYemaHistorico).HasColumnName("huevo_doble_yema_historico").HasDefaultValue(0);
        b.Property(x => x.HuevoDobleYemaDinamico).HasColumnName("huevo_doble_yema_dinamico").HasDefaultValue(0);
        b.Property(x => x.HuevoPisoHistorico).HasColumnName("huevo_piso_historico").HasDefaultValue(0);
        b.Property(x => x.HuevoPisoDinamico).HasColumnName("huevo_piso_dinamico").HasDefaultValue(0);
        b.Property(x => x.HuevoPequenoHistorico).HasColumnName("huevo_pequeno_historico").HasDefaultValue(0);
        b.Property(x => x.HuevoPequenoDinamico).HasColumnName("huevo_pequeno_dinamico").HasDefaultValue(0);
        b.Property(x => x.HuevoRotoHistorico).HasColumnName("huevo_roto_historico").HasDefaultValue(0);
        b.Property(x => x.HuevoRotoDinamico).HasColumnName("huevo_roto_dinamico").HasDefaultValue(0);
        b.Property(x => x.HuevoDesechoHistorico).HasColumnName("huevo_desecho_historico").HasDefaultValue(0);
        b.Property(x => x.HuevoDesechoDinamico).HasColumnName("huevo_desecho_dinamico").HasDefaultValue(0);
        b.Property(x => x.HuevoOtroHistorico).HasColumnName("huevo_otro_historico").HasDefaultValue(0);
        b.Property(x => x.HuevoOtroDinamico).HasColumnName("huevo_otro_dinamico").HasDefaultValue(0);

        b.Property(x => x.HistoricoSemanal).HasColumnName("historico_semanal").HasColumnType("jsonb");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

        b.HasOne(x => x.LotePosturaProduccion)
            .WithMany()
            .HasForeignKey(x => x.LotePosturaProduccionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
