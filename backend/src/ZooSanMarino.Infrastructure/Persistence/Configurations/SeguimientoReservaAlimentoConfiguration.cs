// src/ZooSanMarino.Infrastructure/Persistence/Configurations/SeguimientoReservaAlimentoConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class SeguimientoReservaAlimentoConfiguration : IEntityTypeConfiguration<SeguimientoReservaAlimento>
{
    public void Configure(EntityTypeBuilder<SeguimientoReservaAlimento> e)
    {
        e.ToTable("seguimiento_reserva_alimento", "public");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        e.Property(x => x.PaisId).HasColumnName("pais_id").IsRequired();
        e.Property(x => x.FarmId).HasColumnName("farm_id").IsRequired();
        e.Property(x => x.NucleoId).HasColumnName("nucleo_id").HasMaxLength(50);
        e.Property(x => x.GalponId).HasColumnName("galpon_id").HasMaxLength(50);
        // Sin navegación al silo, por el mismo motivo que en inventario_gestion_stock: se resuelve
        // por servicio y una relación acá arrastraría includes que ninguna lectura necesita.
        e.Property(x => x.SiloId).HasColumnName("silo_id");
        e.Property(x => x.ItemInventarioEcuadorId).HasColumnName("item_inventario_ecuador_id").IsRequired();
        // Camino 1 (catalogo_items) vs camino 2 (item_inventario_ecuador). En Colombia los rangos de
        // id colisionan, así que el origen tiene que viajar con la reserva.
        e.Property(x => x.EsItemInventario).HasColumnName("es_item_inventario").HasDefaultValue(true).IsRequired();

        e.Property(x => x.OrigenModulo).HasColumnName("origen_modulo").HasMaxLength(24).IsRequired();
        e.Property(x => x.OrigenSeguimientoId).HasColumnName("origen_seguimiento_id").IsRequired();
        e.Property(x => x.LoteRef).HasColumnName("lote_ref").HasMaxLength(64);
        e.Property(x => x.FechaSeguimiento).HasColumnName("fecha_seguimiento").IsRequired();

        e.Property(x => x.CantidadKg).HasColumnName("cantidad_kg").HasPrecision(18, 3).IsRequired();
        e.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(20).HasDefaultValue("kg").IsRequired();

        e.Property(x => x.Estado)
            .HasColumnName("estado").HasMaxLength(12)
            .HasDefaultValue(EstadoReservaSeguimiento.Activa).IsRequired();

        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz")
            .HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(64);
        e.Property(x => x.AplicadaAt).HasColumnName("aplicada_at").HasColumnType("timestamptz");
        e.Property(x => x.LiberadaAt).HasColumnName("liberada_at").HasColumnType("timestamptz");

        // La consulta caliente: cuánto hay comprometido en esta ubicación para este ítem. Filtra por
        // estado porque solo las ACTIVAS descuentan disponible.
        e.HasIndex(x => new { x.FarmId, x.ItemInventarioEcuadorId, x.NucleoId, x.GalponId, x.SiloId, x.Estado })
            .HasDatabaseName("ix_seguimiento_reserva_alimento_ubicacion_item_estado");

        // Liberar / aplicar las reservas de un registro concreto.
        e.HasIndex(x => new { x.OrigenModulo, x.OrigenSeguimientoId })
            .HasDatabaseName("ix_seguimiento_reserva_alimento_origen");

        e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_seguimiento_reserva_alimento_company_id");

        e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        // `ItemInventarioEcuadorId` NO lleva FK: es polimórfica (ver la entidad). El discriminador
        // es `EsItemInventario`, y la resolución A→B por código la hace el aplicador al validar.
    }
}
