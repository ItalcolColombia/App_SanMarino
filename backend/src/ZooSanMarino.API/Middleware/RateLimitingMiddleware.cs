// src/ZooSanMarino.API/Middleware/RateLimitingMiddleware.cs
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Routing;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.API.Middleware;

/// <summary>
/// Middleware que limita la cantidad de peticiones por IP para prevenir ataques DDoS
/// y fuerza bruta. La política (límites por ruta, alcance del bloqueo, tiempos) es pura
/// y vive en <see cref="RateLimitingCalculos"/>; acá solo se orquesta HttpContext + caché.
/// Valores tuneables sin redeploy vía sección "RateLimiting" (env RateLimiting__* en ECS).
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IMemoryCache _cache;
    private readonly RateLimitOptions _options;

    // Cache para almacenar contadores de peticiones por IP
    private readonly Dictionary<string, RateLimitInfo> _rateLimitCache = new();
    private readonly object _counterLock = new();
    private DateTime _nextCleanupUtc = DateTime.MinValue;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IMemoryCache cache,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _cache = cache;

        // Defaults pensados para IPs compartidas (oficinas/granjas detrás de NAT): el login
        // tolera varios usuarios por minuto y el bloqueo es corto; la defensa fuerte contra
        // fuerza bruta por cuenta es el lockout de AuthService (5 fallos → bloqueo temporal).
        _options = new RateLimitOptions
        {
            MaxRequestsPerMinute        = configuration.GetValue("RateLimiting:MaxRequestsPerMinute", 100),
            MaxRequestsPerMinuteForAuth = configuration.GetValue("RateLimiting:MaxRequestsPerMinuteForAuth", 15),
            MaxRequestsPerMinuteForSwagger = configuration.GetValue("RateLimiting:MaxRequestsPerMinuteForSwagger", 50),
            // Sincronización offline: se cuenta POR DISPOSITIVO, no por IP, así que el límite
            // puede ser generoso. Un dispositivo que vuelve de un día sin señal drena su cola
            // en lotes; ahogarlo acá solo alarga la ventana en la que el dato de campo vive
            // únicamente en la tablet.
            MaxRequestsPerMinuteForSync = configuration.GetValue("RateLimiting:MaxRequestsPerMinuteForSync", 300),
            BlockDurationMinutes        = configuration.GetValue("RateLimiting:BlockDurationMinutes", 3)
        };
    }

    // Rutas exentas del rate limiter: heartbeat de sesión. Es autenticado (no es vector de fuerza
    // bruta) y se llama periódicamente desde CADA pestaña; contarlo bloquearía la IP compartida de
    // una oficina NAT (muchos usuarios) y tumbaría el acceso de todos.
    private const string HeartbeatPath = "/api/session/heartbeat";

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant().TrimEnd('/') ?? "";

        if (path == HeartbeatPath)
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIpAddress(context);
        var alcance = RateLimitingCalculos.AlcanceDeRuta(path);

        // La identidad de sincronización sale de una cabecera y NO del JWT a propósito: este
        // middleware corre antes de UseAuthentication(), así que context.User todavía está vacío.
        var deviceId = context.Request.Headers[RateLimitingCalculos.DeviceIdHeader].FirstOrDefault();
        var identidad = RateLimitingCalculos.IdentidadCliente(alcance, clientIp, deviceId);

        var limit = RateLimitingCalculos.LimiteParaRuta(
            path,
            _options.MaxRequestsPerMinute,
            _options.MaxRequestsPerMinuteForAuth,
            _options.MaxRequestsPerMinuteForSwagger,
            _options.MaxRequestsPerMinuteForSync);
        var windowSeconds = 60; // Ventana de 1 minuto

        // Agrupar auth evita multiplicar intentos cambiando acción, mayúsculas o slash final.
        // Para el resto usamos la plantilla de ruta: /lotes/1 y /lotes/2 comparten contador.
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? path;
        var bucket = alcance == AlcanceRateLimit.Auth ? "auth" : route.ToLowerInvariant().TrimEnd('/');
        var key = $"{identidad}:{bucket}";
        var now = DateTime.UtcNow;

        // Verificar si aplica un bloqueo vigente según el alcance de la ruta
        foreach (var blockKey in RateLimitingCalculos.ClavesAVerificar(identidad, alcance, clientIp))
        {
            if (!_cache.TryGetValue(blockKey, out DateTime blockUntil)) continue;

            if (blockUntil > now)
            {
                _logger.LogWarning(
                    "IP bloqueada intentando acceder: {ClientIp} desde {Path}. Bloqueo hasta: {BlockUntil}",
                    clientIp, path, blockUntil);

                var remainingSeconds = RateLimitingCalculos.SegundosRestantes(now, blockUntil);
                context.Response.StatusCode = 429; // Too Many Requests
                context.Response.ContentType = "application/json";
                context.Response.Headers["Retry-After"] = remainingSeconds.ToString();
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Too Many Requests",
                    message = $"IP bloqueada temporalmente. Intenta nuevamente en {remainingSeconds} segundos.",
                    retryAfter = remainingSeconds
                });
                return;
            }

            // Desbloquear si ya pasó el tiempo
            _cache.Remove(blockKey);
        }

        int requestCount;
        DateTime windowStart;
        // Creación, cambio de ventana, incremento y limpieza son una sola operación atómica.
        // No hay I/O ni awaits dentro del lock; las ráfagas paralelas no pierden incrementos.
        lock (_counterLock)
        {
            CleanupOldEntries(now);
            if (!_rateLimitCache.TryGetValue(key, out var rateLimitInfo) ||
                RateLimitingCalculos.VentanaExpirada(now, rateLimitInfo.WindowStart, windowSeconds))
            {
                rateLimitInfo = new RateLimitInfo { WindowStart = now };
                _rateLimitCache[key] = rateLimitInfo;
            }
            requestCount = ++rateLimitInfo.RequestCount;
            windowStart = rateLimitInfo.WindowStart;
        }

        // Verificar si excedió el límite
        if (RateLimitingCalculos.ExcedeLimite(requestCount, limit))
        {
            _logger.LogWarning(
                "Rate limit excedido: {Identidad} (ip {ClientIp}) desde {Path}. Intentos: {Count}/{Limit}",
                identidad, clientIp, path, requestCount, limit);

            // Bloquear por el tiempo configurado, con el alcance que corresponda: auth bloquea
            // solo auth de esa IP, sync bloquea solo la sincronización de ESE dispositivo, y el
            // resto bloquea la IP completa.
            var blockKey = RateLimitingCalculos.ClaveBloqueo(identidad, alcance);
            var blockUntilTime = now.AddMinutes(_options.BlockDurationMinutes);
            _cache.Set(blockKey, blockUntilTime, TimeSpan.FromMinutes(_options.BlockDurationMinutes + 1));

            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.ContentType = "application/json";
            context.Response.Headers["Retry-After"] = (_options.BlockDurationMinutes * 60).ToString();

            var mensaje = alcance switch
            {
                AlcanceRateLimit.Auth => $"Demasiados intentos de inicio de sesión desde tu red. Podrás intentar de nuevo en {_options.BlockDurationMinutes} minutos.",
                AlcanceRateLimit.Sync => $"Este dispositivo excedió el límite de sincronización. Reintentá en {_options.BlockDurationMinutes} minutos; la cola no se pierde.",
                _                     => $"Has excedido el límite de peticiones. IP bloqueada por {_options.BlockDurationMinutes} minutos."
            };

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Too Many Requests",
                message = mensaje,
                retryAfter = _options.BlockDurationMinutes * 60
            });
            return;
        }

        // Agregar headers informativos
        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, limit - requestCount).ToString();
        context.Response.Headers["X-RateLimit-Reset"] = windowStart.AddSeconds(windowSeconds).ToString("R");

        await _next(context);
    }

    private static string GetClientIpAddress(HttpContext context) =>
        // UseForwardedHeaders ya verificó que el proxy inmediato sea confiable.
        // Leer X-Forwarded-For/X-Real-IP acá permitiría eludir el límite con una IP inventada.
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private void CleanupOldEntries(DateTime now)
    {
        // Se invoca bajo _counterLock, como máximo una vez por minuto.
        if (now >= _nextCleanupUtc)
        {
            _nextCleanupUtc = now.AddMinutes(1);
            var keysToRemove = new List<string>();
            foreach (var kvp in _rateLimitCache)
            {
                if ((now - kvp.Value.WindowStart).TotalMinutes > 2)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _rateLimitCache.Remove(key);
            }
        }
    }

    private class RateLimitInfo
    {
        public int RequestCount { get; set; }
        public DateTime WindowStart { get; set; }
    }

    private class RateLimitOptions
    {
        public int MaxRequestsPerMinute { get; set; }
        public int MaxRequestsPerMinuteForAuth { get; set; }
        public int MaxRequestsPerMinuteForSwagger { get; set; }
        public int MaxRequestsPerMinuteForSync { get; set; }
        public int BlockDurationMinutes { get; set; }
    }
}

// Extension method para registrar el middleware
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
