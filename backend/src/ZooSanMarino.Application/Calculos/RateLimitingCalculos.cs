// src/ZooSanMarino.Application/Calculos/RateLimitingCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Alcance del bloqueo que produce exceder el límite. Es lo que decide a quién se castiga:
/// no es lo mismo una ráfaga de login (sospechosa) que una tablet drenando su cola de
/// sincronización (esperada y deseable).
/// </summary>
public enum AlcanceRateLimit
{
    /// <summary>Bloquea la IP completa para todas las rutas.</summary>
    General,

    /// <summary>Bloquea solo las rutas de autenticación de esa IP.</summary>
    Auth,

    /// <summary>Bloquea solo la sincronización de ESE dispositivo. Ver <see cref="ClavesAVerificar"/>.</summary>
    Sync
}

/// <summary>
/// Lógica pura del rate limiting (sin HttpContext ni caché): clasificación de rutas,
/// límite aplicable, identidad del cliente, claves de bloqueo por alcance y tiempos.
/// El anti fuerza bruta por cuenta vive aparte en AuthService (5 fallos → lockout temporal);
/// este límite solo amortigua ráfagas, por eso los bloqueos son acotados y cortos.
/// </summary>
public static class RateLimitingCalculos
{
    /// <summary>Cabecera con la que un dispositivo se identifica al sincronizar.</summary>
    public const string DeviceIdHeader = "X-Device-Id";

    /// <summary>Rutas de autenticación pública (login/registro). Se evalúa sobre el path en minúsculas.</summary>
    public static bool EsRutaAuth(string path) =>
        path.Contains("/auth/login") || path.Contains("/auth/register");

    public static bool EsRutaSwagger(string path) =>
        path.StartsWith("/swagger") || path.StartsWith("/swagger-ui");

    /// <summary>Rutas de sincronización offline (pull/push/telemetría de la PWA).</summary>
    public static bool EsRutaSync(string path) =>
        path.StartsWith("/api/sync/") || path == "/api/sync";

    public static int LimiteParaRuta(
        string path,
        int limiteGeneral,
        int limiteAuth,
        int limiteSwagger,
        int limiteSync)
    {
        if (EsRutaAuth(path)) return limiteAuth;
        if (EsRutaSync(path)) return limiteSync;
        if (EsRutaSwagger(path)) return limiteSwagger;
        return limiteGeneral;
    }

    /// <summary>Alcance del bloqueo que corresponde a una ruta.</summary>
    public static AlcanceRateLimit AlcanceDeRuta(string path)
    {
        if (EsRutaAuth(path)) return AlcanceRateLimit.Auth;
        if (EsRutaSync(path)) return AlcanceRateLimit.Sync;
        return AlcanceRateLimit.General;
    }

    /// <summary>
    /// Identidad contra la que se cuenta. En sincronización se cuenta por DISPOSITIVO, no por IP:
    /// cinco tablets de la misma granja comparten el módem, y contarlas juntas hace que se
    /// autobloqueen entre ellas justo cuando vuelve la señal y todas quieren drenar su cola.
    /// Sin cabecera de dispositivo se cae a la IP (fail-safe: peor precisión, nunca ausencia de límite).
    /// </summary>
    public static string IdentidadCliente(AlcanceRateLimit alcance, string clientIp, string? deviceId) =>
        alcance == AlcanceRateLimit.Sync && !string.IsNullOrWhiteSpace(deviceId)
            ? $"dev:{deviceId.Trim()}"
            : clientIp;

    /// <summary>
    /// Clave bajo la cual se registra un bloqueo.
    /// Auth bloquea solo auth para esa IP; Sync bloquea solo la sincronización de ese dispositivo;
    /// General bloquea la IP completa.
    /// </summary>
    public static string ClaveBloqueo(string identidad, AlcanceRateLimit alcance) => alcance switch
    {
        AlcanceRateLimit.Auth => $"blocked:auth:{identidad}",
        AlcanceRateLimit.Sync => $"blocked:sync:{identidad}",
        _                     => $"blocked:{identidad}"
    };

    /// <summary>
    /// Claves de bloqueo que una petición debe respetar.
    ///
    /// General → el bloqueo global de su IP.
    /// Auth    → el global de su IP MÁS el acotado de auth.
    /// Sync    → SOLO el suyo de sincronización, deliberadamente aislado del bloqueo global.
    ///
    /// El aislamiento de Sync es la razón de ser de este alcance: si la sincronización
    /// respetara el bloqueo global, una ráfaga cualquiera desde el módem de la granja dejaría
    /// a todas las tablets sin poder subir capturas de campo, que es exactamente el dato que
    /// no se puede perder. A cambio, un dispositivo autenticado puede usar /api/sync/* sin
    /// contribuir al límite general: aceptable porque esas rutas exigen JWT y su propio límite
    /// se cuenta por dispositivo.
    /// </summary>
    public static string[] ClavesAVerificar(string identidad, AlcanceRateLimit alcance, string clientIp) =>
        alcance switch
        {
            AlcanceRateLimit.Auth => new[] { $"blocked:{clientIp}", $"blocked:auth:{clientIp}" },
            AlcanceRateLimit.Sync => new[] { $"blocked:sync:{identidad}" },
            _                     => new[] { $"blocked:{clientIp}" }
        };

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
