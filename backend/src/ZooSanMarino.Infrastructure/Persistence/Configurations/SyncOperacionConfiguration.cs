using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class SyncOperacionConfiguration : IEntityTypeConfiguration<SyncOperacion>
{
    public void Configure(EntityTypeBuilder<SyncOperacion> builder)
    {
        builder.ToTable("sync_operaciones");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientOpId).IsRequired();
        builder.Property(x => x.Tipo).HasMaxLength(60).IsRequired();
        builder.Property(x => x.DeviceId).HasMaxLength(80);
        builder.Property(x => x.Estado).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ErrorCodigo).HasMaxLength(40);
        builder.Property(x => x.RespuestaJson).HasColumnType("jsonb");
        // F7: sin tope de ancho a propósito, el detalle de una divergencia de stock con varios
        // ítems puede ser largo — no es un código, es texto para el humano de la bandeja.
        builder.Property(x => x.Detalle).HasColumnType("text");

        // El ÚNICO va en la base, no en el service. Es lo que sobrevive a dos dispositivos
        // empujando el mismo lote en paralelo: un chequeo previo en C# tiene una ventana entre el
        // SELECT y el INSERT, y esa ventana es exactamente el caso que la idempotencia debe cubrir.
        builder.HasIndex(x => x.ClientOpId)
            .IsUnique()
            .HasDatabaseName("ux_sync_operaciones_client_op_id");

        // Para la consulta de diagnóstico "qué mandó este dispositivo".
        builder.HasIndex(x => new { x.UserId, x.RecibidoAt })
            .HasDatabaseName("ix_sync_operaciones_user_recibido");

        // F7: la bandeja filtra por empresa + pendientes de resolver. Parcial (WHERE resuelto IS
        // NULL) porque sólo importa el tamaño de lo pendiente, no el histórico ya cuadrado.
        builder.HasIndex(x => new { x.CompanyId, x.Estado })
            .HasDatabaseName("ix_sync_operaciones_bandeja_cuadre")
            .HasFilter("cuadre_resuelto_at IS NULL AND estado = 'requiere_cuadre'");
    }
}
