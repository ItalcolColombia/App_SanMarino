// src/ZooSanMarino.API/Middleware/RateLimitingMiddleware.cs
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimitCache = new();

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
            BlockDurationMinutes        = configuration.GetValue("RateLimiting:BlockDurationMinutes", 3)
        };
    }

    // Rutas exentas del rate limiter: heartbeat de sesión. Es autenticado (no es vector de fuerza
    // bruta) y se llama periódicamente desde CADA pestaña; contarlo bloquearía la IP compartida de
    // una oficina NAT (muchos usuarios) y tumbaría el acceso de todos.
    private const string HeartbeatPath = "/api/session/heartbeat";

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        if (path == HeartbeatPath)
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIpAddress(context);
        var esRutaAuth = RateLimitingCalculos.EsRutaAuth(path);

        var limit = RateLimitingCalculos.LimiteParaRuta(
            path,
            _options.MaxRequestsPerMinute,
            _options.MaxRequestsPerMinuteForAuth,
            _options.MaxRequestsPerMinuteForSwagger);
        var windowSeconds = 60; // Ventana de 1 minuto

        var key = $"{clientIp}:{path}";
        var now = DateTime.UtcNow;

        // Limpiar entradas antiguas periódicamente
        CleanupOldEntries(now);

        // Verificar si aplica un bloqueo vigente (global de la IP y, en rutas de auth, el acotado)
        foreach (var blockKey in RateLimitingCalculos.ClavesAVerificar(clientIp, esRutaAuth))
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

        // Obtener o crear información de rate limit para esta IP
        if (!_rateLimitCache.TryGetValue(key, out var rateLimitInfo))
        {
            rateLimitInfo = new RateLimitInfo
            {
                RequestCount = 0,
                WindowStart = now
            };
            _rateLimitCache.TryAdd(key, rateLimitInfo);
        }

        // Resetear contador si la ventana de tiempo expiró
        if (RateLimitingCalculos.VentanaExpirada(now, rateLimitInfo.WindowStart, windowSeconds))
        {
            rateLimitInfo.RequestCount = 0;
            rateLimitInfo.WindowStart = now;
        }

        // Incrementar contador
        rateLimitInfo.RequestCount++;

        // Verificar si excedió el límite
        if (RateLimitingCalculos.ExcedeLimite(rateLimitInfo.RequestCount, limit))
        {
            _logger.LogWarning(
                "Rate limit excedido: {ClientIp} desde {Path}. Intentos: {Count}/{Limit}",
                clientIp, path, rateLimitInfo.RequestCount, limit);

            // Bloquear por el tiempo configurado: rutas de auth solo bloquean auth para esa IP;
            // el resto bloquea la IP completa.
            var blockKey = RateLimitingCalculos.ClaveBloqueo(clientIp, esRutaAuth);
            var blockUntilTime = now.AddMinutes(_options.BlockDurationMinutes);
            _cache.Set(blockKey, blockUntilTime, TimeSpan.FromMinutes(_options.BlockDurationMinutes + 1));

            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.ContentType = "application/json";
            context.Response.Headers["Retry-After"] = (_options.BlockDurationMinutes * 60).ToString();

            var mensaje = esRutaAuth
                ? $"Demasiados intentos de inicio de sesión desde tu red. Podrás intentar de nuevo en {_options.BlockDurationMinutes} minutos."
                : $"Has excedido el límite de peticiones. IP bloqueada por {_options.BlockDurationMinutes} minutos.";

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
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, limit - rateLimitInfo.RequestCount).ToString();
        context.Response.Headers["X-RateLimit-Reset"] = rateLimitInfo.WindowStart.AddSeconds(windowSeconds).ToString("R");

        await _next(context);
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // Intentar obtener IP real considerando proxies y load balancers
        var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
              ?? context.Request.Headers["X-Real-IP"].FirstOrDefault()
              ?? context.Connection.RemoteIpAddress?.ToString()
              ?? "unknown";

        // Si hay múltiples IPs (X-Forwarded-For puede tener varios), tomar la primera
        if (ip.Contains(','))
        {
            ip = ip.Split(',')[0].Trim();
        }

        return ip;
    }

    private void CleanupOldEntries(DateTime now)
    {
        // Limpiar entradas más antiguas que 2 minutos (solo ocasionalmente para no impactar performance)
        if (Random.Shared.Next(0, 100) < 5) // 5% de probabilidad
        {
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
                _rateLimitCache.TryRemove(key, out _);
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
