// src/ZooSanMarino.Infrastructure/Persistence/Configurations/SeguimientoReservaAvesConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class SeguimientoReservaAvesConfiguration : IEntityTypeConfiguration<SeguimientoReservaAves>
{
    public void Configure(EntityTypeBuilder<SeguimientoReservaAves> e)
    {
        e.ToTable("seguimiento_reserva_aves", "public");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        e.Property(x => x.OrigenModulo).HasColumnName("origen_modulo").HasMaxLength(24).IsRequired();
        e.Property(x => x.OrigenSeguimientoId).HasColumnName("origen_seguimiento_id").IsRequired();

        // Sin FK: cada módulo apunta a una tabla de lotes distinta (lotes, lote_ave_engorde,
        // lote_reproductora_ave_engorde). Una relación acá obligaría a inventar una tabla puente.
        e.Property(x => x.LoteRefInt).HasColumnName("lote_ref_int").IsRequired();
        e.Property(x => x.LoteRef).HasColumnName("lote_ref").HasMaxLength(64);

        e.Property(x => x.FechaSeguimiento).HasColumnName("fecha_seguimiento").IsRequired();

        e.Property(x => x.Hembras).HasColumnName("hembras").HasDefaultValue(0).IsRequired();
        e.Property(x => x.Machos).HasColumnName("machos").HasDefaultValue(0).IsRequired();
        e.Property(x => x.Mixtas).HasColumnName("mixtas").HasDefaultValue(0).IsRequired();

        e.Property(x => x.Estado)
            .HasColumnName("estado").HasMaxLength(12)
            .HasDefaultValue(EstadoReservaSeguimiento.Activa).IsRequired();

        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz")
            .HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(64);
        e.Property(x => x.AplicadaAt).HasColumnName("aplicada_at").HasColumnType("timestamptz");
        e.Property(x => x.LiberadaAt).HasColumnName("liberada_at").HasColumnType("timestamptz");

        // La consulta caliente: cuántas aves tiene comprometidas este lote (para el disponible).
        e.HasIndex(x => new { x.OrigenModulo, x.LoteRefInt, x.Estado })
            .HasDatabaseName("ix_seguimiento_reserva_aves_lote_estado");

        // Liberar / aplicar la reserva de un registro concreto.
        e.HasIndex(x => new { x.OrigenModulo, x.OrigenSeguimientoId })
            .HasDatabaseName("ix_seguimiento_reserva_aves_origen");

        e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_seguimiento_reserva_aves_company_id");

        e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
