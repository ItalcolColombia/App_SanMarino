using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Monitoreo y control de concurrencia/hilos de PostgreSQL desde DB Studio. Solo admin.
/// </summary>
public interface IDbStudioConcurrencyService
{
    Task<ActivitySnapshotDto> GetActivityAsync(CancellationToken ct = default);
    Task<PoolStatsDto> GetPoolStatsAsync(CancellationToken ct = default);
    Task<IEnumerable<LockDto>> GetLocksAsync(CancellationToken ct = default);

    /// <summary>Cancela la consulta en curso de un backend (pg_cancel_backend). Devuelve true si tuvo efecto.</summary>
    Task<bool> CancelBackendAsync(int pid, CancellationToken ct = default);

    /// <summary>Termina una sesión (pg_terminate_backend). Devuelve true si tuvo efecto.</summary>
    Task<bool> TerminateBackendAsync(int pid, CancellationToken ct = default);
}
