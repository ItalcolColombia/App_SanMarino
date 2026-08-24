using ZooSanMarino.Application.DTOs.Sync;

namespace ZooSanMarino.Application.Interfaces;

public interface ISyncPushService
{
    /// <summary>
    /// Aplica un lote de operaciones capturadas sin red. Devuelve un resultado POR operación:
    /// una rechazada no bloquea a las demás.
    /// </summary>
    Task<SyncPushResponse> PushAsync(SyncPushRequest request, CancellationToken ct = default);

    /// <summary>
    /// F7 — la bandeja: filas <c>requiere_cuadre</c> de la empresa activa que nadie marcó vistas
    /// todavía. Más nuevo primero (lo reciente es lo que alguien puede corregir hoy en el galpón).
    /// </summary>
    Task<List<CuadrePendienteDto>> ListarCuadresPendientesAsync(CancellationToken ct = default);

    /// <summary>
    /// F7 — marca una fila de la bandeja como vista. SOLO marca visto: no repone kilos ni toca el
    /// stock (decisión de negocio — reponer acá sería una segunda fórmula para el mismo número).
    /// <c>false</c> = no existe, no es <c>requiere_cuadre</c>, ya estaba resuelta, o es de otra
    /// empresa (fail-closed: nunca resuelve la fila de una empresa que no es la de la sesión).
    /// </summary>
    Task<bool> ResolverCuadreAsync(long id, CancellationToken ct = default);
}
