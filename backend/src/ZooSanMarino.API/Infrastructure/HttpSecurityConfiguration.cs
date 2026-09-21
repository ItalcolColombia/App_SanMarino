using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace ZooSanMarino.API.Infrastructure;

/// <summary>Confianza de transporte explícita, compartida por el arranque y sus pruebas HTTP.</summary>
public static class HttpSecurityConfiguration
{
    public static void AddTrustedProxyHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = configuration.GetValue("ReverseProxy:ForwardLimit", 1);
            if (options.ForwardLimit is < 1 or > 10)
                throw new InvalidOperationException("ReverseProxy:ForwardLimit debe estar entre 1 y 10.");

            // Se conservan los defaults de loopback. NUNCA vaciar ambas listas: significaría
            // confiar en cualquier origen. En ECS configurar las subredes reales del ALB.
            foreach (var value in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
            {
                if (!IPAddress.TryParse(value, out var address))
                    throw new InvalidOperationException("ReverseProxy:KnownProxies contiene una IP inválida.");
                options.KnownProxies.Add(address);
            }
            foreach (var value in configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
            {
                if (!System.Net.IPNetwork.TryParse(value, out var network) || network.PrefixLength == 0)
                    throw new InvalidOperationException("ReverseProxy:KnownNetworks requiere redes CIDR acotadas.");
                options.KnownIPNetworks.Add(network);
            }
        });
    }

    public static void AddCorsFromOrigins(this IServiceCollection services, string policyName, string[] origins)
    {
        var normalized = origins.Select(origin => origin.Trim().TrimEnd('/')).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var origin in normalized)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" ||
                uri.Query.Length != 0 || uri.Fragment.Length != 0 || origin.Contains('*'))
                throw new InvalidOperationException("AllowedOrigins sólo admite orígenes HTTP(S) explícitos, sin rutas ni comodines.");
        }

        services.AddCors(options => options.AddPolicy(policyName, policy =>
        {
            // Vacío = ningún origen cross-origin autorizado. Las apps nativas no dependen de CORS.
            if (normalized.Length > 0) policy.WithOrigins(normalized);
            policy.AllowAnyMethod().AllowAnyHeader()
                .WithExposedHeaders("X-Auth-Failure", "Retry-After", "X-RateLimit-Limit",
                    "X-RateLimit-Remaining", "X-RateLimit-Reset", "Content-Disposition");
        }));
    }
}
