// src/ZooSanMarino.API/Middleware/SecurityHeadersMiddleware.cs
using Microsoft.AspNetCore.Hosting;

namespace ZooSanMarino.API.Middleware;

/// <summary>
/// Middleware que agrega headers de seguridad HTTP para proteger la aplicación
/// contra diversos tipos de ataques (XSS, clickjacking, MIME sniffing, etc.).
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Agregar headers de seguridad
        var response = context.Response;

        // Prevenir clickjacking
        // X-Frame-Options: DENY previene que la página sea incrustada en iframes
        response.Headers["X-Frame-Options"] = "DENY";

        // Prevenir MIME type sniffing
        // X-Content-Type-Options: nosniff previene que el navegador adivine el tipo MIME
        response.Headers["X-Content-Type-Options"] = "nosniff";

        // Habilitar XSS protection (modo bloqueo)
        // X-XSS-Protection: Protección adicional contra XSS en navegadores antiguos
        response.Headers["X-XSS-Protection"] = "1; mode=block";

        // Política de referrer (no enviar referrer en requests externos)
        // Referrer-Policy: Controla qué información de referrer se envía
        response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Content Security Policy (CSP) - Previene XSS e inyección de contenido
        // Nota: 'unsafe-inline' y 'unsafe-eval' son necesarios para Swagger UI
        // En producción, considerar usar nonce-based CSP para mayor seguridad
        // frame-ancestors 'none' previene clickjacking (más moderno que X-Frame-Options)
        var csp = "default-src 'self'; " +
                  "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " + // Swagger necesita unsafe-inline/unsafe-eval
                  "style-src 'self' 'unsafe-inline'; " + // Swagger necesita unsafe-inline
                  "img-src 'self' data: https:; " +
                  "font-src 'self' data:; " +
                  "connect-src 'self' https:; " + // Permitir conexiones HTTPS para APIs externas
                  "frame-ancestors 'none'; " + // Previene clickjacking
                  "base-uri 'self'; " + // Previene inyección de base tag
                  "form-action 'self'; " + // Previene envío de formularios a dominios externos
                  "upgrade-insecure-requests;"; // Fuerza HTTPS para recursos HTTP
        
        response.Headers["Content-Security-Policy"] = csp;
        
        // Report-URI para CSP (opcional, requiere endpoint de reporte)
        // response.Headers["Content-Security-Policy-Report-Only"] = csp + " report-uri /api/csp-report;";

        // Permissions Policy (anteriormente Feature-Policy)
        // Controla qué APIs del navegador están disponibles
        response.Headers["Permissions-Policy"] = 
            "geolocation=(), microphone=(), camera=(), payment=(), usb=(), magnetometer=(), gyroscope=()";

        // Rate Limiting Headers (informativos)
        // Estos headers informan al cliente sobre los límites de rate limiting
        // Los valores reales se configuran en el middleware de rate limiting
        response.Headers["X-RateLimit-Limit"] = "100";
        response.Headers["X-RateLimit-Remaining"] = "99";
        response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString();

        // Prevenir que el navegador cachee respuestas sensibles
        // (se puede sobrescribir en endpoints específicos si es necesario)
        if (context.Request.Path.StartsWithSegments("/api/auth") || 
            context.Request.Path.StartsWithSegments("/api"))
        {
            response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            response.Headers["Pragma"] = "no-cache";
            response.Headers["Expires"] = "0";
        }

        // Strict-Transport-Security (HSTS) - Fuerza uso de HTTPS
        // Se aplica en producción cuando se detecta HTTPS (directo o a través de proxy)
        var isProduction = _environment.IsProduction();
        var isHttps = context.Request.IsHttps;
        
        // Verificar también X-Forwarded-Proto para detectar HTTPS cuando está detrás de un proxy/load balancer
        var forwardedProto = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var isHttpsViaProxy = string.Equals(forwardedProto, "https", StringComparison.OrdinalIgnoreCase);
        
        if (isProduction && (isHttps || isHttpsViaProxy))
        {
            // HSTS solo debe aplicarse cuando hay HTTPS
            // max-age=31536000 = 1 año
            // includeSubDomains = aplica a todos los subdominios
            // preload = permite incluir en listas de HSTS del navegador
            response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
        }

        // Remover headers que puedan revelar información del servidor
        // Esto previene que atacantes obtengan información sobre la versión del servidor
        response.Headers.Remove("Server");
        response.Headers.Remove("X-Powered-By");
        response.Headers.Remove("X-AspNet-Version");
        response.Headers.Remove("X-AspNetMvc-Version");

        // Agregar header para prevenir clickjacking adicional
        response.Headers["X-Download-Options"] = "noopen";
        
        // Prevenir que Internet Explorer ejecute archivos descargados
        response.Headers["X-DNS-Prefetch-Control"] = "off";

        await _next(context);
    }
}

// Extension method para registrar el middleware
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}

