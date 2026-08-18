using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class AlimentoEntregaCicloEngordeConfiguration : IEntityTypeConfiguration<AlimentoEntregaCicloEngorde>
{
    public void Configure(EntityTypeBuilder<AlimentoEntregaCicloEngorde> b)
    {
        b.ToTable("alimento_entrega_ciclo_engorde");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.FarmId).HasColumnName("farm_id").IsRequired();

        // Cadena vacia, no NULL: la fn compara la ubicacion con COALESCE(TRIM(...), ''), asi que si
        // aca el nucleo fuera NULL el JOIN por ubicacion no encontraria la entrega.
        b.Property(x => x.NucleoId).HasColumnName("nucleo_id").HasMaxLength(64).IsRequired().HasDefaultValue("");
        b.Property(x => x.GalponId).HasColumnName("galpon_id").HasMaxLength(64).IsRequired();

        b.Property(x => x.OrigenTabla).HasColumnName("origen_tabla").HasMaxLength(80).IsRequired();
        b.Property(x => x.OrigenId).HasColumnName("origen_id").IsRequired();
        b.Property(x => x.HistId).HasColumnName("hist_id");

        b.Property(x => x.FechaMovimiento).HasColumnName("fecha_movimiento").HasColumnType("date").IsRequired();
        b.Property(x => x.KgMovimiento).HasColumnName("kg_movimiento").HasColumnType("numeric(18,3)").IsRequired();
        b.Property(x => x.NumeroDocumento).HasColumnName("numero_documento").HasMaxLength(200);

        b.Property(x => x.LoteCedenteId).HasColumnName("lote_cedente_id");
        b.Property(x => x.LoteDestinoId).HasColumnName("lote_destino_id");
        b.Property(x => x.FechaEntrega).HasColumnName("fecha_entrega").HasColumnType("date");

        b.Property(x => x.KgEntregados).HasColumnName("kg_entregados").HasColumnType("numeric(18,3)").IsRequired();
        b.Property(x => x.KgNoDiferible).HasColumnName("kg_no_diferible").HasColumnType("numeric(18,3)").IsRequired();

        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).IsRequired();
        b.Property(x => x.Motivo).HasColumnName("motivo").HasMaxLength(400);
        b.Property(x => x.Sellada).HasColumnName("sellada").IsRequired();

        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(100);
        b.Property(x => x.AnuladaAt).HasColumnName("anulada_at");
        b.Property(x => x.AnuladaPorUserId).HasColumnName("anulada_por_user_id").HasMaxLength(100);
        b.Property(x => x.AnuladaMotivo).HasColumnName("anulada_motivo").HasMaxLength(400);

        // Un movimiento tiene UNA entrega viva. Las anuladas no cuentan: el historico se ANULA, nunca
        // se borra, asi que tienen que poder acumularse sin chocar con la nueva.
        b.HasIndex(x => new { x.OrigenTabla, x.OrigenId })
            .HasDatabaseName("uq_entrega_ciclo_origen")
            .IsUnique()
            .HasFilter("estado <> 'ANULADA'");

        b.HasIndex(x => new { x.FarmId, x.NucleoId, x.GalponId, x.FechaMovimiento })
            .HasDatabaseName("ix_entrega_ciclo_ubicacion");

        // Parciales: la fn entra por aca una vez por lote, y solo le interesan las VIGENTE.
        b.HasIndex(x => x.LoteCedenteId)
            .HasDatabaseName("ix_entrega_ciclo_cedente")
            .HasFilter("estado = 'VIGENTE'");

        b.HasIndex(x => x.LoteDestinoId)
            .HasDatabaseName("ix_entrega_ciclo_destino")
            .HasFilter("estado = 'VIGENTE'");
    }
}
