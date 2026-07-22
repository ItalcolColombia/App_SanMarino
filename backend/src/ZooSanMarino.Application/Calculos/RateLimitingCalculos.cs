// src/ZooSanMarino.Application/Calculos/RateLimitingCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica pura del rate limiting por IP (sin HttpContext ni caché): clasificación de rutas,
/// límite aplicable, claves de bloqueo por alcance (auth vs IP completa) y tiempos.
/// El anti fuerza bruta por cuenta vive aparte en AuthService (5 fallos → lockout temporal);
/// este límite solo amortigua ráfagas por IP, por eso el bloqueo de auth es acotado y corto.
/// </summary>
public static class RateLimitingCalculos
{
    /// <summary>Rutas de autenticación pública (login/registro). Se evalúa sobre el path en minúsculas.</summary>
    public static bool EsRutaAuth(string path) =>
        path.Contains("/auth/login") || path.Contains("/auth/register");

    public static bool EsRutaSwagger(string path) =>
        path.StartsWith("/swagger") || path.StartsWith("/swagger-ui");

    public static int LimiteParaRuta(string path, int limiteGeneral, int limiteAuth, int limiteSwagger)
    {
        if (EsRutaAuth(path)) return limiteAuth;
        if (EsRutaSwagger(path)) return limiteSwagger;
        return limiteGeneral;
    }

    /// <summary>
    /// Clave bajo la cual se registra un bloqueo. Exceder el límite en rutas de auth bloquea
    /// SOLO auth para esa IP (una oficina con NAT no pierde el resto de la app); exceder un
    /// límite general bloquea la IP completa.
    /// </summary>
    public static string ClaveBloqueo(string clientIp, bool esRutaAuth) =>
        esRutaAuth ? $"blocked:auth:{clientIp}" : $"blocked:{clientIp}";

    /// <summary>
    /// Claves que una petición debe respetar: toda ruta respeta el bloqueo global de la IP;
    /// las rutas de auth respetan además su bloqueo acotado.
    /// </summary>
    public static string[] ClavesAVerificar(string clientIp, bool esRutaAuth) =>
        esRutaAuth
            ? new[] { $"blocked:{clientIp}", $"blocked:auth:{clientIp}" }
            : new[] { $"blocked:{clientIp}" };

    /// <summary>Segundos restantes de bloqueo, redondeados hacia arriba y nunca negativos.</summary>
    public static int SegundosRestantes(DateTime ahoraUtc, DateTime bloqueadoHastaUtc)
    {
        var restante = (int)Math.Ceiling((bloqueadoHastaUtc - ahoraUtc).TotalSeconds);
        return Math.Max(restante, 0);
    }

    /// <summary>El límite se excede estrictamente por encima (contador == límite todavía pasa).</summary>
    public static bool ExcedeLimite(int contadorActual, int limite) => contadorActual > limite;

    public static bool VentanaExpirada(DateTime ahoraUtc, DateTime inicioVentanaUtc, int ventanaSegundos) =>
        (ahoraUtc - inicioVentanaUtc).TotalSeconds >= ventanaSegundos;
}
