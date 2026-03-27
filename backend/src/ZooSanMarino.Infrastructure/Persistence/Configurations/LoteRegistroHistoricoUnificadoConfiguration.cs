using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LoteRegistroHistoricoUnificadoConfiguration : IEntityTypeConfiguration<LoteRegistroHistoricoUnificado>
{
    public void Configure(EntityTypeBuilder<LoteRegistroHistoricoUnificado> b)
    {
        b.ToTable("lote_registro_historico_unificado");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        b.Property(x => x.LoteAveEngordeId).HasColumnName("lote_ave_engorde_id");
        b.Property(x => x.FarmId).HasColumnName("farm_id").IsRequired();

        b.Property(x => x.NucleoId).HasColumnName("nucleo_id").HasMaxLength(64);
        b.Property(x => x.GalponId).HasColumnName("galpon_id").HasMaxLength(64);

        b.Property(x => x.FechaOperacion).HasColumnName("fecha_operacion").HasColumnType("date").IsRequired();
        b.Property(x => x.TipoEvento).HasColumnName("tipo_evento").HasMaxLength(40).IsRequired();

        b.Property(x => x.OrigenTabla).HasColumnName("origen_tabla").HasMaxLength(80).IsRequired();
        b.Property(x => x.OrigenId).HasColumnName("origen_id").IsRequired();

        b.Property(x => x.MovementTypeOriginal).HasColumnName("movement_type_original").HasMaxLength(40);
        b.Property(x => x.ItemInventarioEcuadorId).HasColumnName("item_inventario_ecuador_id");
        b.Property(x => x.ItemResumen).HasColumnName("item_resumen").HasMaxLength(400);

        b.Property(x => x.CantidadKg).HasColumnName("cantidad_kg").HasColumnType("numeric(18,3)");
        b.Property(x => x.Unidad).HasColumnName("unidad").HasMaxLength(20);

        b.Property(x => x.CantidadHembras).HasColumnName("cantidad_hembras");
        b.Property(x => x.CantidadMachos).HasColumnName("cantidad_machos");
        b.Property(x => x.CantidadMixtas).HasColumnName("cantidad_mixtas");

        b.Property(x => x.Referencia).HasColumnName("referencia").HasMaxLength(500);
        b.Property(x => x.NumeroDocumento).HasColumnName("numero_documento").HasMaxLength(200);
        b.Property(x => x.AcumuladoEntradasAlimentoKg).HasColumnName("acumulado_entradas_alimento_kg").HasColumnType("numeric(18,3)");

        b.Property(x => x.Anulado).HasColumnName("anulado").IsRequired();

        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
    }
}

