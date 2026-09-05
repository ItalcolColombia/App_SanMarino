using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.API.Middleware;

/// <summary>
/// Puerta de acceso a Swagger: sin contraseña no se ve ni la UI ni el <c>swagger.json</c>.
/// Sólo se monta fuera de Production (ver <c>Program.cs</c>, bloque 14.1-14.4).
///
/// <para>
/// Este archivo <b>orquesta</b> (lee cookies, escribe la respuesta); la decisión —qué se protege,
/// si la contraseña vale, si la sesión venció— vive en
/// <see cref="SwaggerAccesoCalculos"/>, pura y con tests. Hasta el 4-sep-2026 estaba acá y
/// <b>duplicada</b> en un endpoint inline de <c>Program.cs</c>: el que emitía la cookie y el que la
/// validaba calculaban el mismo hash por separado, así que tocar uno solo dejaba al equipo entero
/// afuera sin ningún mensaje que lo explicara.
/// </para>
/// </summary>
public class SwaggerPasswordMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SwaggerPasswordMiddleware> _logger;
    private readonly string? _expectedPassword;
    private readonly string _cookieName;

    public SwaggerPasswordMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<SwaggerPasswordMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        // Sin respaldo hardcodeado a propósito: antes había una constante con la contraseña real
        // acá y en Program.cs, así que vaciar la configuración no cerraba la puerta —la abría con
        // una contraseña que está en el repositorio. Sin configuración, no entra nadie.
        _expectedPassword = configuration[ConfigPassword];
        _cookieName = configuration[ConfigCookie] ?? CookiePorDefecto;
    }

    public const string ConfigPassword = "Swagger:Password";
    public const string ConfigCookie = "Swagger:SessionCookieName";
    public const string CookiePorDefecto = "SwaggerAuth";

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        if (!SwaggerAccesoCalculos.EsRutaProtegida(path))
        {
            await _next(context);
            return;
        }

        // El POST del propio formulario lo atiende Program.cs; interceptarlo dejaría la puerta
        // cerrada por dentro.
        if (SwaggerAccesoCalculos.EsRutaExenta(context.Request.Method, path))
        {
            await _next(context);
            return;
        }

        if (SesionVigente(context))
        {
            // Deslizante: cada petición corre el vencimiento otros 6 minutos.
            EmitirSesion(context, _expectedPassword!, _cookieName);
            await _next(context);
            return;
        }

        await MostrarFormularioAsync(context);
    }

    private bool SesionVigente(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(_cookieName, out var valor)) return false;

        return SwaggerAccesoCalculos.CookieVigente(
            valor,
            _expectedPassword,
            context.Connection.RemoteIpAddress?.ToString(),
            DateTime.UtcNow);
    }

    /// <summary>
    /// Emite (o renueva) la cookie de sesión. Es <c>static</c> y pública porque el endpoint
    /// <c>POST /swagger/login</c> de <c>Program.cs</c> la usa: una sola función escribe la cookie,
    /// así no puede volver a pasar que emisor y validador se desincronicen.
    /// </summary>
    public static void EmitirSesion(HttpContext context, string passwordEsperada, string cookieName)
    {
        // El ALB termina TLS y habla HTTP con la tarea: sin mirar X-Forwarded-Proto, la cookie
        // saldría sin Secure detrás del proxy.
        var forwardedProto = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var esSeguro = context.Request.IsHttps ||
                       string.Equals(forwardedProto, "https", StringComparison.OrdinalIgnoreCase);

        var vencimiento = DateTimeOffset.UtcNow.AddMinutes(SwaggerAccesoCalculos.MinutosInactividad);

        context.Response.Cookies.Append(
            cookieName,
            SwaggerAccesoCalculos.EmitirCookie(
                passwordEsperada,
                context.Connection.RemoteIpAddress?.ToString(),
                DateTime.UtcNow),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = esSeguro,
                SameSite = SameSiteMode.Strict,
                Expires = vencimiento,
                Path = "/"
            });
    }

    private async Task MostrarFormularioAsync(HttpContext context)
    {
        // 🔒 El mensaje llega por query string y se pinta dentro del HTML: sin escapar, cualquier
        // enlace `/swagger?error=<etiqueta>` ejecutaba script en el navegador de quien lo abriera.
        var errorCrudo = context.Request.Query["error"].ToString();
        var error = HtmlEncoder.Default.Encode(errorCrudo);

        if (string.IsNullOrEmpty(_expectedPassword))
        {
            _logger.LogError(
                "Swagger está montado pero '{Clave}' no está configurada: el acceso queda cerrado.",
                ConfigPassword);
            error = HtmlEncoder.Default.Encode(
                $"Swagger no tiene contraseña configurada ({ConfigPassword}). Avisá al equipo.");
        }

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

        {(string.IsNullOrWhiteSpace(error) ? "" : $"<div class='error'>{error}</div>")}

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
}
