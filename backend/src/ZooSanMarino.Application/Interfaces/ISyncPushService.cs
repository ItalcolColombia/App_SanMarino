using ZooSanMarino.Application.DTOs.Sync;

namespace ZooSanMarino.Application.Interfaces;

public interface ISyncPushService
{
    /// <summary>
    /// Aplica un lote de operaciones capturadas sin red. Devuelve un resultado POR operación:
    /// una rechazada no bloquea a las demás.
    /// </summary>
    Task<SyncPushResponse> PushAsync(SyncPushRequest request, CancellationToken ct = default);
}
