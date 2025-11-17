// src/ZooSanMarino.API/Middleware/PlatformSecretMiddleware.cs
using Microsoft.Extensions.Configuration;
using ZooSanMarino.Infrastructure.Services;

namespace ZooSanMarino.API.Middleware;

/// <summary>
/// Middleware que valida el SECRET_UP en todas las peticiones HTTP para asegurar
/// que solo las peticiones del frontend autorizado puedan acceder a los endpoints.
/// El SECRET_UP viene encriptado y debe ser desencriptado antes de validarlo.
/// </summary>
public class PlatformSecretMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _expectedSecret;
    private readonly string _encryptionKey;
    private readonly ILogger<PlatformSecretMiddleware> _logger;
    private readonly EncryptionService _encryptionService;

    public PlatformSecretMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<PlatformSecretMiddleware> logger,
        EncryptionService encryptionService)
    {
        _next = next;
        _expectedSecret = configuration["PlatformSecret:SecretUpFrontend"]
            ?? throw new InvalidOperationException("PlatformSecret:SecretUpFrontend no configurada");
        _encryptionKey = configuration["PlatformSecret:EncryptionKey"]
            ?? throw new InvalidOperationException("PlatformSecret:EncryptionKey no configurada");
        _logger = logger;
        _encryptionService = encryptionService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Permitir peticiones OPTIONS (preflight de CORS) sin validación
        if (context.Request.Method == "OPTIONS")
        {
            await _next(context);
            return;
        }

        // Permitir endpoints públicos (sin SECRET_UP) si es necesario
        // Por ejemplo, endpoints de health check, ping, o Swagger
        var path = context.Request.Path.Value?.ToLower() ?? "";
        
        // Excluir rutas de Swagger (están protegidas por SwaggerPasswordMiddleware)
        if (path.StartsWith("/swagger") || path.StartsWith("/swagger-ui"))
        {
            await _next(context);
            return;
        }
        
        // Excluir otros endpoints públicos
        if (path.Contains("/ping") || path.Contains("/health"))
        {
            await _next(context);
            return;
        }
        
        // Excluir endpoints de autenticación públicos (login/register/recover-password sin SECRET_UP inicialmente)
        // Nota: El login puede requerir SECRET_UP dependiendo de la implementación
        if (path.Contains("/auth/login") || path.Contains("/auth/register") || path.Contains("/auth/recover-password"))
        {
            await _next(context);
            return;
        }

        // Obtener el SECRET_UP encriptado del header
        var encryptedSecretUp = context.Request.Headers["X-Secret-Up"].FirstOrDefault()
                             ?? context.Request.Headers["X-SECRET-UP"].FirstOrDefault()
                             ?? context.Request.Headers["Secret-Up"].FirstOrDefault()
                             ?? context.Request.Headers["SECRET-UP"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(encryptedSecretUp))
        {
            _logger.LogWarning("Petición rechazada: falta header X-Secret-Up desde {RemoteIpAddress}", 
                context.Connection.RemoteIpAddress);
            
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = "SECRET_UP no proporcionado en el header X-Secret-Up"
            });
            return;
        }

        // Desencriptar el SECRET_UP recibido
        string decryptedSecretUp;
        try
        {
            decryptedSecretUp = _encryptionService.Decrypt(encryptedSecretUp, _encryptionKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, 
                "Petición rechazada: Error al desencriptar SECRET_UP desde {RemoteIpAddress}", 
                context.Connection.RemoteIpAddress);
            
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = "Error al desencriptar SECRET_UP"
            });
            return;
        }

        // Validar que el SECRET_UP desencriptado coincida con el esperado
        if (decryptedSecretUp != _expectedSecret)
        {
            _logger.LogWarning(
                "Petición rechazada: SECRET_UP inválido desde {RemoteIpAddress}. Esperado: {ExpectedPrefix}..., Recibido: {ReceivedPrefix}...",
                context.Connection.RemoteIpAddress,
                _expectedSecret.Substring(0, Math.Min(10, _expectedSecret.Length)),
                decryptedSecretUp.Substring(0, Math.Min(10, decryptedSecretUp.Length)));
            
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = "SECRET_UP inválido"
            });
            return;
        }

        // SECRET_UP válido, continuar con la petición
        await _next(context);
    }
}

// Extension method para registrar el middleware fácilmente
public static class PlatformSecretMiddlewareExtensions
{
    public static IApplicationBuilder UsePlatformSecret(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PlatformSecretMiddleware>();
    }
}

