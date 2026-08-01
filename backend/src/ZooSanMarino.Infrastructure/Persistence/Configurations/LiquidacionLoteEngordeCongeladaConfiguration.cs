using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cabecera de la liquidación congelada de engorde. El DETALLE
/// (<c>liquidacion_lote_engorde_congelada_fila</c>) se crea por SQL en la migración y NO se mapea:
/// la única lectura es dentro de <c>fn_seguimiento_diario_engorde</c> v13.
/// </summary>
public class LiquidacionLoteEngordeCongeladaConfiguration : IEntityTypeConfiguration<LiquidacionLoteEngordeCongelada>
{
    public void Configure(EntityTypeBuilder<LiquidacionLoteEngordeCongelada> b)
    {
        b.ToTable("liquidacion_lote_engorde_congelada");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.LoteAveEngordeId).HasColumnName("lote_ave_engorde_id").IsRequired();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.GranjaId).HasColumnName("granja_id").IsRequired();
        b.Property(x => x.LiquidadoAt).HasColumnName("liquidado_at").IsRequired();
        b.Property(x => x.LiquidadoPorUserId).HasColumnName("liquidado_por_user_id").IsRequired();
        b.Property(x => x.CongeladaAt).HasColumnName("congelada_at").IsRequired();
        b.Property(x => x.Origen).HasColumnName("origen").IsRequired().HasMaxLength(30);
        b.Property(x => x.FnVersion).HasColumnName("fn_version").IsRequired().HasMaxLength(30);
        b.Property(x => x.Filas).HasColumnName("filas").IsRequired();
        b.Property(x => x.Checksum).HasColumnName("checksum").IsRequired().HasMaxLength(64);

        // Resumen aprobado (NULL en copias de backfill ⇒ el resumen cae a vivo)
        b.Property(x => x.LoteNombre).HasColumnName("lote_nombre").IsRequired();
        b.Property(x => x.EstadoOperativoLote).HasColumnName("estado_operativo_lote").IsRequired().HasMaxLength(20);
        b.Property(x => x.HembrasInicio).HasColumnName("hembras_inicio");
        b.Property(x => x.MachosInicio).HasColumnName("machos_inicio");
        b.Property(x => x.MixtasInicio).HasColumnName("mixtas_inicio");
        b.Property(x => x.TotalAvesInicio).HasColumnName("total_aves_inicio");
        b.Property(x => x.VentasTotalHembras).HasColumnName("ventas_total_hembras");
        b.Property(x => x.VentasTotalMachos).HasColumnName("ventas_total_machos");
        b.Property(x => x.VentasTotalMixtas).HasColumnName("ventas_total_mixtas");
        b.Property(x => x.AvesVivasActuales).HasColumnName("aves_vivas_actuales");
        b.Property(x => x.MovimientosVentaCount).HasColumnName("movimientos_venta_count");
        b.Property(x => x.SaldoAlimentoKg).HasColumnName("saldo_alimento_kg").HasPrecision(18, 3);
        b.Property(x => x.MermaUnidades).HasColumnName("merma_unidades");
        b.Property(x => x.MermaKilos).HasColumnName("merma_kilos").HasPrecision(18, 3);

        b.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

        b.Property(x => x.AnuladaAt).HasColumnName("anulada_at");
        b.Property(x => x.AnuladaPorUserId).HasColumnName("anulada_por_user_id");
        b.Property(x => x.AnuladaMotivo).HasColumnName("anulada_motivo");

        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");

        // ⭐ El invariante: a lo sumo UNA copia VIGENTE por lote. Es lo que vuelve imposible que
        // convivan dos fotos del mismo lote (dos cierres concurrentes, retry del front, etc.).
        // El precedente Panamá ya usa único por lote; liquidacion_cierre_lote_levante lo omitió y
        // por eso su upsert puede duplicar — ese defecto no se copia.
        b.HasIndex(x => x.LoteAveEngordeId)
         .IsUnique()
         .HasFilter("anulada_at IS NULL")
         .HasDatabaseName("ux_liquidacion_lote_engorde_congelada_vigente");

        // Historial de versiones por lote / auditoría por empresa
        b.HasIndex(x => new { x.LoteAveEngordeId, x.CongeladaAt })
         .IsDescending(false, true)
         .HasDatabaseName("ix_liquidacion_lote_engorde_congelada_lote");
        b.HasIndex(x => new { x.CompanyId, x.CongeladaAt })
         .IsDescending(false, true)
         .HasDatabaseName("ix_liquidacion_lote_engorde_congelada_company");

        b.HasOne(x => x.LoteAveEngorde)
         .WithMany()
         .HasForeignKey(x => x.LoteAveEngordeId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
