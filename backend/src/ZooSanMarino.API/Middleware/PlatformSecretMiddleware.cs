// src/ZooSanMarino.API/Middleware/PlatformSecretMiddleware.cs
using Microsoft.Extensions.Configuration;
using ZooSanMarino.Infrastructure.Services;

namespace ZooSanMarino.API.Middleware;

/// <summary>
/// Middleware que valida el SECRET_UP en todas las peticiones HTTP para asegurar
/// que solo las peticiones del frontend autorizado puedan acceder a los endpoints.
/// El SECRET_UP viene encriptado y debe ser desencriptado antes de validarlo.
///
/// <para>
/// Sus rechazos son 401 igual que los de autenticación, pero <b>no significan lo mismo</b>:
/// éste dice "no reconozco de dónde viene la petición", no "tu sesión venció". El cliente
/// los distingue por la cabecera <see cref="AuthFailureHeader"/> = <see cref="PlatformFailureValue"/>.
/// Sin esa distinción, el interceptor del front cierra la sesión ante cualquier 401: rotar el
/// SECRET_UP destruiría la sesión —y, cuando exista, la cola de sincronización offline— de
/// todos los dispositivos a la vez.
/// </para>
/// </summary>
public class PlatformSecretMiddleware
{
    /// <summary>Cabecera que tipifica POR QUÉ se rechazó, para que el cliente no adivine.</summary>
    public const string AuthFailureHeader = "X-Auth-Failure";

    /// <summary>Valor que marca un rechazo de plataforma (origen), NO de autenticación.</summary>
    public const string PlatformFailureValue = "platform-secret";

    /// <summary>
    /// Cabecera donde queda anotado QUÉ cliente resultó válido, para el resto del
    /// pipeline (logs, auditoría). No la manda el cliente: la escribe este
    /// middleware después de verificar la firma, así que no se puede falsear.
    /// </summary>
    public const string ClienteHeader = "X-Platform-Client";

    public const string ClienteWeb = "web";
    public const string ClienteMovil = "movil";

    private readonly RequestDelegate _next;
    private readonly string _expectedSecret;

    /// <summary>
    /// Firma propia de la app móvil. `null` cuando el ambiente todavía no la
    /// configuró: en ese caso la app sigue entrando con la del front y nada se
    /// rompe. Ver la nota de compatibilidad en <see cref="InvokeAsync"/>.
    /// </summary>
    private readonly string? _expectedSecretMovil;

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
        // A propósito NO lanza si falta: un ambiente que todavía no la definió
        // tiene que seguir andando. Se puede fijar por appsettings o por variable
        // de entorno (`PlatformSecret__SecretUpMovil`), que es como se le pasa en
        // producción sin commitearla.
        _expectedSecretMovil = configuration["PlatformSecret:SecretUpMovil"];
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

        // Excluir peticiones autenticadas con un Service Token (PAT: "Bearer sk_...").
        // Autorizado por el dueño (2026-07-03): el SECRET_UP es un gate de origen-frontend que un
        // cliente de servidor (cron/bot) no posee. La credencial del bot es el propio PAT —aleatorio,
        // revocable y scopeado SOLO a /api/tickets, validado en el esquema "ServiceToken"—; sin un PAT
        // válido igual se responde 401. Trasladamos el gate del SECRET_UP al PAT.
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader is not null &&
            authHeader.StartsWith("Bearer sk_", StringComparison.OrdinalIgnoreCase))
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
            
            await RechazarAsync(context, "SECRET_UP no proporcionado en el header X-Secret-Up");
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
            
            await RechazarAsync(context, "Error al desencriptar SECRET_UP");
            return;
        }

        // Cada cliente tiene su propia firma. Antes había una sola compartida, así
        // que el backend no podía distinguir de dónde venía la petición y rotarla
        // dejaba sin servicio al web y a la app a la vez. Ahora se puede revocar
        // la app sin tocar el front, y el log dice cuál falló.
        //
        // Compatibilidad: mientras un ambiente no defina la firma móvil, la app
        // entra con la del front y todo sigue igual. Nada deja de andar por
        // desplegar este cambio antes de configurar el secreto.
        string? cliente = null;
        if (decryptedSecretUp == _expectedSecret)
        {
            cliente = ClienteWeb;
        }
        else if (!string.IsNullOrWhiteSpace(_expectedSecretMovil) &&
                 decryptedSecretUp == _expectedSecretMovil)
        {
            cliente = ClienteMovil;
        }

        if (cliente is null)
        {
            // Nunca se loguea el secreto recibido, ni un prefijo: era un oráculo
            // que filtraba de a diez caracteres el valor esperado en cada intento.
            _logger.LogWarning(
                "Petición rechazada: firma de plataforma inválida desde {RemoteIpAddress} hacia {Path}",
                context.Connection.RemoteIpAddress, path);

            await RechazarAsync(context, "SECRET_UP inválido");
            return;
        }

        // Queda anotado para el resto del pipeline. Se sobrescribe cualquier valor
        // que hubiera mandado el cliente: acá sólo escribe quien verificó la firma.
        context.Request.Headers[ClienteHeader] = cliente;

        // SECRET_UP válido, continuar con la petición
        await _next(context);
    }

    /// <summary>
    /// Rechazo de plataforma. Mantiene el 401 y el cuerpo históricos (`error` + `message`)
    /// y agrega la tipificación: la cabecera <see cref="AuthFailureHeader"/> y `errorCode`
    /// en el cuerpo. El cliente usa eso para NO cerrar la sesión del usuario por un
    /// problema que no tiene nada que ver con sus credenciales.
    /// </summary>
    private static async Task RechazarAsync(HttpContext context, string mensaje)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        context.Response.Headers[AuthFailureHeader] = PlatformFailureValue;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Unauthorized",
            errorCode = PlatformFailureValue,
            message = mensaje
        });
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

