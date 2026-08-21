using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// B1 — el registro de sesiones vivas. Es una <b>lista blanca</b>: el login inserta la fila y cada
/// request exige que exista, no esté revocada y no haya vencido.
///
/// <para>
/// La decisión de si una sesión sigue viva es pura (<see cref="RevocacionSesionCalculos"/>); acá sólo
/// va la persistencia y la caché.
/// </para>
/// </summary>
public interface ISesionActivaService
{
    /// <summary>
    /// Registra la sesión que acaba de emitir el login. Idempotente por <c>jti</c>: si la fila ya
    /// existe no la duplica (el índice único la protege igual).
    /// </summary>
    Task RegistrarAsync(
        Guid jti, Guid userId, DateTime expiresAtUtc,
        string? deviceId, string? ipAddress, string? userAgent, CancellationToken ct);

    /// <summary>
    /// ¿Sigue viva la sesión de este token? Se llama en el camino de TODO request autenticado, así
    /// que va cacheada.
    /// <b>Ante un fallo de BD devuelve <see cref="EstadoSesion.NoVerificable"/></b> (o sea: deja
    /// pasar) — es la excepción deliberada al fail-closed, documentada en el service. Un token sin
    /// <c>jti</c> devuelve <see cref="EstadoSesion.Legado"/>, que desde V39.13 <b>no</b> pasa.
    /// </summary>
    Task<EstadoSesion> EvaluarAsync(string? jti, DateTime expiracionToken, CancellationToken ct);

    /// <summary>
    /// Marca contacto real del dispositivo (<c>last_seen_at</c>), con throttle. Sólo lo llama el
    /// heartbeat: un <c>UPDATE</c> por request sería peor que el <c>SELECT</c> que la caché evita.
    /// </summary>
    Task TocarAsync(string? jti, CancellationToken ct);

    /// <summary>Revoca una sesión por id. <c>false</c> si no existe o ya estaba revocada.</summary>
    Task<bool> RevocarAsync(long id, Guid? revocadaPor, string? motivo, CancellationToken ct);

    /// <summary>
    /// Revoca TODAS las sesiones vivas de un usuario. Devuelve cuántas apagó.
    /// Es lo que corre al cambiar la contraseña o al dar de baja al usuario.
    /// </summary>
    Task<int> RevocarTodasDelUsuarioAsync(Guid userId, Guid? revocadaPor, string? motivo, CancellationToken ct);

    /// <summary>Sesiones de un usuario (las vivas primero). <paramref name="jtiActual"/> marca cuál es la de quien mira.</summary>
    Task<IReadOnlyList<SesionActivaDto>> ListarDeUsuarioAsync(
        Guid userId, string? jtiActual, bool incluirRevocadas, CancellationToken ct);

    /// <summary>La fila cruda de una sesión, para poder decidir si quien pide es su dueño.</summary>
    Task<(long Id, Guid UserId)?> ObtenerDuenoAsync(long id, CancellationToken ct);

    /// <summary>
    /// Borra las filas vencidas hace más de 30 días. Se invoca de forma perezosa (una vez por hora
    /// como mucho) desde el heartbeat: no hay <c>HostedService</c> en el proyecto y no se introduce
    /// un patrón nuevo por esto.
    /// </summary>
    Task<int> LimpiarVencidasAsync(CancellationToken ct);
}
