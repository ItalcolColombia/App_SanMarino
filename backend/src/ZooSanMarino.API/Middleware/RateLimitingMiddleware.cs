// src/ZooSanMarino.API/Middleware/RateLimitingMiddleware.cs
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace ZooSanMarino.API.Middleware;

/// <summary>
/// Middleware que limita la cantidad de peticiones por IP para prevenir ataques DDoS
/// y fuerza bruta.
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
        IMemoryCache cache)
    {
        _next = next;
        _logger = logger;
        _cache = cache;
        
        // Configuración por defecto (ajustada para balance entre seguridad y usabilidad)
        _options = new RateLimitOptions
        {
            MaxRequestsPerMinute = 100,         // 100 peticiones por minuto por IP (aumentado para usuarios legítimos)
            MaxRequestsPerMinuteForAuth = 5,    // 5 intentos de login por minuto (mantiene seguridad)
            MaxRequestsPerMinuteForSwagger = 50, // 50 peticiones por minuto para Swagger
            BlockDurationMinutes = 10           // Bloquear IP por 10 minutos si excede límites (reducido)
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";
        var clientIp = GetClientIpAddress(context);

        // Obtener límite según el tipo de endpoint
        var limit = GetRateLimitForPath(path);
        var windowSeconds = 60; // Ventana de 1 minuto

        var key = $"{clientIp}:{path}";
        var now = DateTime.UtcNow;

        // Limpiar entradas antiguas periódicamente
        CleanupOldEntries(now);

        // Verificar si la IP está bloqueada
        var blockKey = $"blocked:{clientIp}";
        if (_cache.TryGetValue(blockKey, out DateTime blockUntil))
        {
            if (blockUntil > now)
            {
                _logger.LogWarning(
                    "IP bloqueada intentando acceder: {ClientIp} desde {Path}. Bloqueo hasta: {BlockUntil}",
                    clientIp, path, blockUntil);

                context.Response.StatusCode = 429; // Too Many Requests
                context.Response.ContentType = "application/json";
                var remainingSeconds = (int)(blockUntil - now).TotalSeconds;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Too Many Requests",
                    message = $"IP bloqueada temporalmente. Intenta nuevamente en {remainingSeconds} segundos.",
                    retryAfter = remainingSeconds
                });
                return;
            }
            else
            {
                // Desbloquear si ya pasó el tiempo
                _cache.Remove(blockKey);
            }
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
        if ((now - rateLimitInfo.WindowStart).TotalSeconds >= windowSeconds)
        {
            rateLimitInfo.RequestCount = 0;
            rateLimitInfo.WindowStart = now;
        }

        // Incrementar contador
        rateLimitInfo.RequestCount++;

        // Verificar si excedió el límite
        if (rateLimitInfo.RequestCount > limit)
        {
            _logger.LogWarning(
                "Rate limit excedido: {ClientIp} desde {Path}. Intentos: {Count}/{Limit}",
                clientIp, path, rateLimitInfo.RequestCount, limit);

            // Bloquear IP por el tiempo configurado
            var blockUntilTime = now.AddMinutes(_options.BlockDurationMinutes);
            _cache.Set(blockKey, blockUntilTime, TimeSpan.FromMinutes(_options.BlockDurationMinutes + 1));

            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.ContentType = "application/json";
            context.Response.Headers["Retry-After"] = (_options.BlockDurationMinutes * 60).ToString();
            
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Too Many Requests",
                message = $"Has excedido el límite de peticiones. IP bloqueada por {_options.BlockDurationMinutes} minutos.",
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

    private int GetRateLimitForPath(string path)
    {
        if (path.Contains("/auth/login") || path.Contains("/auth/register"))
        {
            return _options.MaxRequestsPerMinuteForAuth;
        }
        
        if (path.StartsWith("/swagger") || path.StartsWith("/swagger-ui"))
        {
            return _options.MaxRequestsPerMinuteForSwagger;
        }

        return _options.MaxRequestsPerMinute;
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

