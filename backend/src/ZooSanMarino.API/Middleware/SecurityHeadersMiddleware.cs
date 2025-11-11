// src/ZooSanMarino.API/Middleware/SecurityHeadersMiddleware.cs
namespace ZooSanMarino.API.Middleware;

/// <summary>
/// Middleware que agrega headers de seguridad HTTP para proteger la aplicación
/// contra diversos tipos de ataques.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Agregar headers de seguridad
        var response = context.Response;

        // Prevenir clickjacking
        response.Headers["X-Frame-Options"] = "DENY";

        // Prevenir MIME type sniffing
        response.Headers["X-Content-Type-Options"] = "nosniff";

        // Habilitar XSS protection (modo bloqueo)
        response.Headers["X-XSS-Protection"] = "1; mode=block";

        // Política de referrer (no enviar referrer en requests externos)
        response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Content Security Policy (CSP) - ajustar según necesidades
        var csp = "default-src 'self'; " +
                  "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " + // Swagger necesita unsafe-inline/unsafe-eval
                  "style-src 'self' 'unsafe-inline'; " + // Swagger necesita unsafe-inline
                  "img-src 'self' data: https:; " +
                  "font-src 'self' data:; " +
                  "connect-src 'self'; " +
                  "frame-ancestors 'none';";
        
        response.Headers["Content-Security-Policy"] = csp;

        // Permissions Policy (anteriormente Feature-Policy)
        response.Headers["Permissions-Policy"] = 
            "geolocation=(), microphone=(), camera=(), payment=(), usb=(), magnetometer=(), gyroscope=()";

        // Prevenir que el navegador cachee respuestas sensibles
        // (se puede sobrescribir en endpoints específicos si es necesario)
        if (context.Request.Path.StartsWithSegments("/api/auth") || 
            context.Request.Path.StartsWithSegments("/api"))
        {
            response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            response.Headers["Pragma"] = "no-cache";
            response.Headers["Expires"] = "0";
        }

        // En producción con HTTPS, usar cookies seguras
        if (context.Request.IsHttps)
        {
            // Headers adicionales para producción HTTPS
            response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
        }

        // Remover headers que puedan revelar información del servidor
        response.Headers.Remove("Server");
        response.Headers.Remove("X-Powered-By");

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

