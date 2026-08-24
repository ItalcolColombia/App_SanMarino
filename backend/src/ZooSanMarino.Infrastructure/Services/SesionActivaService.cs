using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// B1 — persistencia y caché de las sesiones vivas. Toda decisión la toma
/// <see cref="RevocacionSesionCalculos"/> (pura y testeada); acá sólo se traen y se escriben datos.
///
/// <para>
/// <b>El costo por request y cómo se paga.</b> <see cref="EvaluarAsync"/> corre en el camino de TODO
/// request autenticado. Se apoya en <see cref="IMemoryCache"/> (ya registrado en <c>Program.cs</c>):
/// una sesión válida se cachea 60 s —esa es la cota de cuánto tarda una revocación en surtir efecto
/// por tarea ECS— y una muerta se cachea hasta el <c>exp</c> del token, porque no puede resucitar.
/// <c>last_seen_at</c> NO se escribe acá: sólo desde el heartbeat y con throttle.
/// </para>
/// </summary>
public class SesionActivaService : ISesionActivaService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SesionActivaService> _logger;

    /// <summary>Prefijo de la clave de caché del veredicto por <c>jti</c>.</summary>
    private const string ClaveEstado = "sesion:";

    /// <summary>Prefijo de la marca anti-escritura de <c>last_seen_at</c>.</summary>
    private const string ClaveTocada = "sesion:tocada:";

    /// <summary>Gate de la limpieza perezosa: como mucho una pasada por hora y por tarea.</summary>
    private const string ClaveLimpieza = "sesion:limpieza";

    /// <summary>Retención de filas ya vencidas. Pasado eso no sirven ni para auditoría de sesión viva.</summary>
    private static readonly TimeSpan RetencionVencidas = TimeSpan.FromDays(30);

    public SesionActivaService(
        ZooSanMarinoContext ctx,
        IMemoryCache cache,
        ILogger<SesionActivaService> logger)
    {
        _ctx = ctx;
        _cache = cache;
        _logger = logger;
    }

    public async Task RegistrarAsync(
        Guid jti, Guid userId, DateTime expiresAtUtc,
        string? deviceId, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        if (jti == Guid.Empty || userId == Guid.Empty)
            return;

        // Idempotente por jti: el índice único lo garantiza, pero preguntar evita la excepción.
        if (await _ctx.SesionesActivas.AsNoTracking().AnyAsync(s => s.Jti == jti, ct))
            return;

        _ctx.SesionesActivas.Add(new SesionActiva
        {
            Jti = jti,
            UserId = userId,
            DeviceId = Recortar(deviceId, 100),
            IpAddress = Recortar(ipAddress, 64),
            UserAgent = Recortar(userAgent, 300),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = ANormalizarUtc(expiresAtUtc),
        });

        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<EstadoSesion> EvaluarAsync(string? jti, DateTime expiracionToken, CancellationToken ct)
    {
        // Token sin `jti`. Hasta V39.13 esto era la ventana de gracia del despliegue de B1 y pasaba;
        // desde el 21-ago-2026 se RECHAZA (la decisión vive en RevocacionSesionCalculos, que es lo
        // testeado). No se consulta nada: sin `jti` no hay fila que buscar.
        if (string.IsNullOrWhiteSpace(jti))
            return EstadoSesion.Legado;

        var clave = ClaveEstado + jti;
        if (_cache.TryGetValue<EstadoSesion>(clave, out var cacheado))
            return cacheado;

        var ahora = DateTime.UtcNow;
        EstadoSesion estado;

        try
        {
            if (!Guid.TryParse(jti, out var jtiGuid))
            {
                // Un jti que no es un Guid no lo emitió este backend: no puede tener fila.
                estado = EstadoSesion.NoRegistrada;
            }
            else
            {
                var fila = await _ctx.SesionesActivas
                    .AsNoTracking()
                    .Where(s => s.Jti == jtiGuid)
                    .Select(s => new { s.RevokedAt, s.ExpiresAt })
                    .FirstOrDefaultAsync(ct);

                estado = RevocacionSesionCalculos.Evaluar(
                    jti,
                    hayFila: fila is not null,
                    revokedAt: fila?.RevokedAt,
                    expiresAt: fila?.ExpiresAt,
                    ahoraUtc: ahora);
            }
        }
        catch (Exception ex)
        {
            // ⛔ EXCEPCIÓN DELIBERADA AL FAIL-CLOSED. Si RDS se cae, rechazar todo convertiría una
            // caída de base en el deslogueo simultáneo de todas las tablets en campo —con sus colas
            // de capturas sin subir—, que es peor que el riesgo que evita. Se acepta el token y se
            // loguea. No se cachea: al volver la BD, el request siguiente ya verifica de verdad.
            // Estado PROPIO (`NoVerificable`, V39.13): hasta entonces compartía valor con `Legado`,
            // y cerrar la ventana de gracia sin separarlos habría hecho que un blip de RDS
            // deslogueara a todo el mundo — exactamente el desastre que esta rama existe para evitar.
            _logger.LogError(ex,
                "No se pudo verificar la sesión contra la base. Se acepta el token (fail-open deliberado).");
            return EstadoSesion.NoVerificable;
        }

        _cache.Set(clave, estado, RevocacionSesionCalculos.TtlCache(estado, ANormalizarUtc(expiracionToken), ahora));
        return estado;
    }

    public async Task TocarAsync(string? jti, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(jti) || !Guid.TryParse(jti, out var jtiGuid))
            return;

        // Primera barrera: sin tocar la base. El heartbeat llega cada 90 s.
        var claveTocada = ClaveTocada + jti;
        if (_cache.TryGetValue(claveTocada, out _))
            return;

        try
        {
            var fila = await _ctx.SesionesActivas.FirstOrDefaultAsync(s => s.Jti == jtiGuid, ct);
            if (fila is null) return;

            var ahora = DateTime.UtcNow;
            if (RevocacionSesionCalculos.DebeActualizarUltimaVista(fila.LastSeenAt, ahora))
            {
                fila.LastSeenAt = ahora;
                await _ctx.SaveChangesAsync(ct);
            }

            _cache.Set(claveTocada, true, RevocacionSesionCalculos.UmbralUltimaVistaPorDefecto);
        }
        catch (Exception ex)
        {
            // `last_seen_at` es auditoría, no autorización: que falle no puede tumbar el heartbeat.
            _logger.LogWarning(ex, "No se pudo actualizar last_seen_at de la sesión.");
        }
    }

    public async Task<bool> RevocarAsync(long id, Guid? revocadaPor, string? motivo, CancellationToken ct)
    {
        var fila = await _ctx.SesionesActivas.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (fila is null || fila.RevokedAt is not null)
            return false;

        fila.RevokedAt = DateTime.UtcNow;
        fila.RevokedByUserId = revocadaPor;
        fila.RevokedReason = Recortar(motivo, 200);
        await _ctx.SaveChangesAsync(ct);

        InvalidarCache(fila.Jti, fila.ExpiresAt);
        return true;
    }

    public async Task<int> RevocarTodasDelUsuarioAsync(Guid userId, Guid? revocadaPor, string? motivo, CancellationToken ct)
    {
        if (userId == Guid.Empty) return 0;

        var ahora = DateTime.UtcNow;
        var vivas = await _ctx.SesionesActivas
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > ahora)
            .ToListAsync(ct);

        if (vivas.Count == 0) return 0;

        foreach (var fila in vivas)
        {
            fila.RevokedAt = ahora;
            fila.RevokedByUserId = revocadaPor;
            fila.RevokedReason = Recortar(motivo, 200);
        }

        await _ctx.SaveChangesAsync(ct);

        foreach (var fila in vivas)
            InvalidarCache(fila.Jti, fila.ExpiresAt);

        return vivas.Count;
    }

    public async Task<IReadOnlyList<SesionActivaDto>> ListarDeUsuarioAsync(
        Guid userId, string? jtiActual, bool incluirRevocadas, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;

        var q = _ctx.SesionesActivas.AsNoTracking().Where(s => s.UserId == userId);
        if (!incluirRevocadas)
            q = q.Where(s => s.RevokedAt == null && s.ExpiresAt > ahora);

        var filas = await q
            .OrderByDescending(s => s.LastSeenAt ?? s.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        return filas.Select(s => ToDto(s, jtiActual)).ToList();
    }

    public async Task<(long Id, Guid UserId)?> ObtenerDuenoAsync(long id, CancellationToken ct)
    {
        var fila = await _ctx.SesionesActivas.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.Id, s.UserId })
            .FirstOrDefaultAsync(ct);

        return fila is null ? null : (fila.Id, fila.UserId);
    }

    public async Task<int> LimpiarVencidasAsync(CancellationToken ct)
    {
        // Perezosa: como mucho una pasada por hora y por tarea. Sin HostedService — no hay ninguno
        // en el proyecto y no se introduce un patrón nuevo por una limpieza de retención.
        if (_cache.TryGetValue(ClaveLimpieza, out _))
            return 0;

        _cache.Set(ClaveLimpieza, true, TimeSpan.FromHours(1));

        try
        {
            var corte = DateTime.UtcNow - RetencionVencidas;
            return await _ctx.SesionesActivas
                .Where(s => s.ExpiresAt < corte)
                .ExecuteDeleteAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron limpiar las sesiones vencidas.");
            return 0;
        }
    }

    /// <summary>
    /// Deja el veredicto muerto en la caché de ESTA tarea hasta el <c>exp</c>. En las demás tareas
    /// ECS la revocación tarda lo que reste del TTL de 60 s: por eso la UI promete «menos de un
    /// minuto», no «inmediato».
    /// </summary>
    private void InvalidarCache(Guid jti, DateTime expiresAt)
    {
        var clave = ClaveEstado + jti.ToString();
        var ahora = DateTime.UtcNow;
        var ttl = RevocacionSesionCalculos.TtlCache(EstadoSesion.Revocada, ANormalizarUtc(expiresAt), ahora);

        if (ttl > TimeSpan.Zero)
            _cache.Set(clave, EstadoSesion.Revocada, ttl);
        else
            _cache.Remove(clave);

        _cache.Remove(ClaveTocada + jti.ToString());
    }

    private static SesionActivaDto ToDto(SesionActiva s, string? jtiActual) => new(
        s.Id,
        Etiqueta(s.Jti),
        s.DeviceId,
        s.IpAddress,
        s.UserAgent,
        s.CreatedAt,
        s.ExpiresAt,
        s.LastSeenAt,
        s.RevokedAt,
        s.RevokedReason,
        EsLaActual: !string.IsNullOrWhiteSpace(jtiActual) &&
                    string.Equals(jtiActual, s.Jti.ToString(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Últimos 8 caracteres del <c>jti</c>: alcanza para distinguir dos sesiones sin publicar la llave.</summary>
    private static string Etiqueta(Guid jti)
    {
        var texto = jti.ToString("N");
        return texto[^8..];
    }

    private static string? Recortar(string? valor, int max)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var limpio = valor.Trim();
        return limpio.Length <= max ? limpio : limpio[..max];
    }

    /// <summary>Npgsql exige <c>Kind = Utc</c> para <c>timestamptz</c>.</summary>
    private static DateTime ANormalizarUtc(DateTime valor) => valor.Kind switch
    {
        DateTimeKind.Utc => valor,
        DateTimeKind.Local => valor.ToUniversalTime(),
        _ => DateTime.SpecifyKind(valor, DateTimeKind.Utc),
    };
}
