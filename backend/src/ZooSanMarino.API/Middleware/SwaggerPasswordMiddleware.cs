using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace ZooSanMarino.API.Middleware;

/// <summary>
/// Middleware que protege Swagger con una contraseña.
/// Muestra un formulario de login si no se ha autenticado.
/// </summary>
public class SwaggerPasswordMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SwaggerPasswordMiddleware> _logger;
    private readonly string _expectedPassword;
    private readonly string _cookieName;

    public SwaggerPasswordMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<SwaggerPasswordMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
        _expectedPassword = configuration["Swagger:Password"] ?? "Swagger2024!SanMarino#API";
        _cookieName = configuration["Swagger:SessionCookieName"] ?? "SwaggerAuth";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // Solo proteger rutas de Swagger
        if (!path.StartsWith("/swagger") && !path.StartsWith("/swagger-ui"))
        {
            await _next(context);
            return;
        }

        // NO interceptar el endpoint de login POST (lo maneja Program.cs)
        if (context.Request.Method == "POST" && (path == "/swagger/login" || path.Contains("/swagger/login")))
        {
            await _next(context);
            return;
        }

        // Verificar si ya está autenticado (cookie válida)
        // Si está autenticado, permitir acceso a TODO Swagger (UI, JSON, recursos estáticos, etc.)
        if (IsAuthenticated(context))
        {
            await _next(context);
            return;
        }

        // Usuario NO autenticado: mostrar formulario de login
        // Esto bloquea: /swagger, /swagger/index.html, /swagger/v1/swagger.json, /swagger-ui/*, etc.
        await ShowLoginPageAsync(context);
    }

    private bool IsAuthenticated(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(_cookieName, out var cookieValue))
            return false;

        try
        {
            // Validar el hash de la cookie
            var expectedHash = ComputeHash(_expectedPassword + context.Connection.RemoteIpAddress?.ToString());
            if (cookieValue != expectedHash)
                return false;

            // Verificar si hay timestamp de última actividad
            var lastActivityKey = $"{_cookieName}_LastActivity";
            if (context.Request.Cookies.TryGetValue(lastActivityKey, out var lastActivityStr))
            {
                if (DateTime.TryParse(lastActivityStr, out var lastActivity))
                {
                    var inactivityTimeout = TimeSpan.FromMinutes(6); // 6 minutos de inactividad
                    var timeSinceLastActivity = DateTime.UtcNow - lastActivity;

                    // Si ha pasado más de 6 minutos, la sesión expira
                    if (timeSinceLastActivity > inactivityTimeout)
                    {
                        // Limpiar cookies
                        context.Response.Cookies.Delete(_cookieName);
                        context.Response.Cookies.Delete(lastActivityKey);
                        return false;
                    }
                }
            }

            // Renovar la sesión si está activa (sliding expiration)
            RenewSession(context);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RenewSession(HttpContext context)
    {
        var cookieName = _cookieName;
        var lastActivityKey = $"{_cookieName}_LastActivity";
        
        // Crear hash de autenticación
        var hash = SHA256.Create().ComputeHash(
            Encoding.UTF8.GetBytes(_expectedPassword + context.Connection.RemoteIpAddress?.ToString()));
        var hashString = Convert.ToBase64String(hash);

        // Detectar HTTPS vía proxy
        var forwardedProto = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var isHttpsViaProxy = string.Equals(forwardedProto, "https", StringComparison.OrdinalIgnoreCase);
        var isSecure = context.Request.IsHttps || isHttpsViaProxy;
        
        // Opciones de cookie con 6 minutos de expiración
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true, // Previene acceso desde JavaScript
            Secure = isSecure, // Solo enviar por HTTPS
            SameSite = SameSiteMode.Strict, // Más estricto para cookies de autenticación
            Expires = DateTimeOffset.UtcNow.AddMinutes(6), // 6 minutos desde ahora
            Path = "/" // Aplicar a todo el sitio
        };

        // Renovar cookie de autenticación
        context.Response.Cookies.Append(cookieName, hashString, cookieOptions);

        // Actualizar timestamp de última actividad
        var lastActivityOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure, // Debe ser Secure también
            SameSite = SameSiteMode.Strict, // Más estricto
            Expires = DateTimeOffset.UtcNow.AddMinutes(6), // 6 minutos desde ahora
            Path = "/"
        };
        context.Response.Cookies.Append(lastActivityKey, DateTime.UtcNow.ToString("O"), lastActivityOptions);
    }

    private async Task ShowLoginPageAsync(HttpContext context)
    {
        // Leer mensaje de error de la query string si existe
        var errorMessage = context.Request.Query["error"].ToString();
        
        var html = $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Autenticación Swagger - ZooSanMarino</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%);
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            padding: 20px;
        }}
        .container {{
            background: #1e293b;
            border-radius: 12px;
            padding: 40px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5);
            max-width: 450px;
            width: 100%;
            border: 1px solid #334155;
        }}
        .logo {{
            text-align: center;
            margin-bottom: 30px;
        }}
        .logo h1 {{
            color: #e2e8f0;
            font-size: 28px;
            font-weight: 600;
            margin-bottom: 8px;
        }}
        .logo p {{
            color: #94a3b8;
            font-size: 14px;
        }}
        .form-group {{
            margin-bottom: 20px;
        }}
        label {{
            display: block;
            color: #cbd5e1;
            font-size: 14px;
            font-weight: 500;
            margin-bottom: 8px;
        }}
        input[type='password'] {{
            width: 100%;
            padding: 12px 16px;
            background: #0f172a;
            border: 1px solid #334155;
            border-radius: 8px;
            color: #e2e8f0;
            font-size: 16px;
            transition: all 0.2s;
        }}
        input[type='password']:focus {{
            outline: none;
            border-color: #3b82f6;
            box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
        }}
        .btn {{
            width: 100%;
            padding: 12px;
            background: #3b82f6;
            color: white;
            border: none;
            border-radius: 8px;
            font-size: 16px;
            font-weight: 600;
            cursor: pointer;
            transition: background 0.2s;
            margin-top: 10px;
        }}
        .btn:hover {{
            background: #2563eb;
        }}
        .btn:active {{
            background: #1d4ed8;
        }}
        .error {{
            background: #7f1d1d;
            border: 1px solid #991b1b;
            color: #fca5a5;
            padding: 12px;
            border-radius: 8px;
            margin-bottom: 20px;
            font-size: 14px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='logo'>
            <h1>🔐 Swagger API</h1>
            <p>ZooSanMarino - Acceso Protegido</p>
        </div>

        {(string.IsNullOrWhiteSpace(errorMessage) ? "" : $"<div class='error'>{errorMessage}</div>")}

        <form method='POST' action='/swagger/login'>
            <div class='form-group'>
                <label for='password'>🔑 Contraseña de Acceso</label>
                <input type='password' id='password' name='password' placeholder='Ingresa la contraseña' required autofocus>
            </div>
            <button type='submit' class='btn'>Acceder a Swagger</button>
        </form>
    </div>
</body>
</html>";

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html);
    }

    private string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

