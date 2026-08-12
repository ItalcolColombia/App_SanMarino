using System.Text.Json;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.DTOs.Sync;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SyncPushService
{
    /// <summary>
    /// Alta de seguimiento diario de PRODUCCIÓN (la segunda etapa de postura; la primera es levante).
    ///
    /// Igual que en levante, llama al mismo service que usa el controller: una captura offline pasa
    /// exactamente por donde pasa una con red.
    ///
    /// ## El autor y la empresa ya los pone el servidor
    ///
    /// `ProduccionService` estampa `CompanyId` y `CreatedByUserId` desde el token, y busca el lote
    /// filtrando por la empresa de la sesión. Por eso una operación de otra empresa no puede escribir
    /// acá aunque se cuele: no encontraría el lote. La comprobación explícita de
    /// `SyncPushCalculos` existe para que el motivo del rechazo sea el real y no un "Lote no existe",
    /// que en campo se lee como un dato corrupto.
    /// </summary>
    private async Task<(int?, string?)> CrearSeguimientoProduccionAsync(SyncOperacionRequest op, CancellationToken ct)
    {
        if (op.Payload is not { } payload || payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("La operación no trae cuerpo.");
        }

        var request = payload.Deserialize<CrearSeguimientoRequest>(OpcionesJson)
                      ?? throw new InvalidOperationException("El cuerpo de la captura no se pudo interpretar.");

        var id = await _produccion.CrearSeguimientoAsync(request);
        ct.ThrowIfCancellationRequested();

        return (id, JsonSerializer.Serialize(new { id }, OpcionesJson));
    }
}
