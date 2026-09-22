using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ZooSanMarino.API.Middleware;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Application.Options;

namespace ZooSanMarino.API.Infrastructure;

/// <summary>Contrato JWT compartido por el arranque y las pruebas del pipeline HTTP.</summary>
public static class JwtAuthenticationConfiguration
{
    private const string EstadoSesionItem = "jwt:estado-sesion";

    public static void Configure(JwtBearerOptions options, JwtOptions jwt)
    {
        jwt.EnsureValid();
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IgnoreTrailingSlashWhenValidatingAudience = false,
            // La actual y, tras una rotación, la anterior: los tokens emitidos antes del deploy
            // siguen valiendo hasta vencer. Se firma siempre con la actual (AuthService).
            IssuerSigningKeys = JwtRotacionClaveCalculos.ClavesDeValidacion(jwt.Key, jwt.PreviousKey)
                .Select(clave => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)))
                .ToArray(),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (HttpMethods.IsOptions(context.Request.Method))
                    context.NoResult();
                return Task.CompletedTask;
            },
            OnTokenValidated = ValidateSessionAsync,
            OnChallenge = WriteSessionFailureAsync
        };
    }

    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var sessions = context.HttpContext.RequestServices.GetRequiredService<ISesionActivaService>();
        var state = await sessions.EvaluarAsync(
            jti, context.SecurityToken.ValidTo, context.HttpContext.RequestAborted);

        if (RevocacionSesionCalculos.EsSesionValida(state))
            return;

        context.HttpContext.Items[EstadoSesionItem] = state;
        context.Fail("No se pudo acreditar una sesión vigente.");
    }

    private static async Task WriteSessionFailureAsync(JwtBearerChallengeContext context)
    {
        if (context.HttpContext.Items[EstadoSesionItem] is not EstadoSesion state)
            return;

        context.HandleResponse();
        context.Response.Headers.CacheControl = "no-store";
        var unavailable = state == EstadoSesion.NoVerificable;
        var reason = RevocacionSesionCalculos.MotivoParaCliente(state);

        if (unavailable)
        {
            // Indisponibilidad no es revocación: los clientes mantienen sesión y cola offline.
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "5";
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers[PlatformSecretMiddleware.AuthFailureHeader] = reason;
        }

        await context.Response.WriteAsJsonAsync(new
        {
            error = unavailable ? "ServiceUnavailable" : "Unauthorized",
            errorCode = reason,
            message = unavailable
                ? "No fue posible verificar la sesión. Intenta nuevamente en unos segundos."
                : reason == RevocacionSesionCalculos.MotivoRevocada
                    ? "La sesión fue cerrada. Inicia sesión de nuevo."
                    : "La sesión expiró. Inicia sesión de nuevo."
        }, cancellationToken: context.HttpContext.RequestAborted);
    }
}
