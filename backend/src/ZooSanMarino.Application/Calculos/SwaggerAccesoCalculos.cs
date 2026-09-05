// src/ZooSanMarino.Application/Calculos/SwaggerAccesoCalculos.cs
using System.Security.Cryptography;
using System.Text;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Decisión pura del gate de acceso a Swagger: qué rutas protege, si la contraseña es la
/// correcta y si la sesión sigue viva. Sin <c>HttpContext</c>, sin configuración, sin cookies:
/// el middleware es el que lee y escribe; acá sólo se decide.
///
/// <para>
/// Existe porque la lógica vivía imperativa dentro de <c>SwaggerPasswordMiddleware</c> y en un
/// endpoint inline de <c>Program.cs</c> — <b>duplicada</b>. La huella de la sesión se calculaba en
/// los dos lados: el que emite la cookie y el que la valida. Cambiar uno solo dejaba a todo el
/// equipo afuera de Swagger sin un mensaje que lo explicara.
/// </para>
///
/// <para>
/// El gate NO reemplaza a la autenticación del API: sólo decide quién puede <i>ver</i> la
/// documentación. Cada endpoint sigue exigiendo su JWT y su firma de plataforma.
/// </para>
/// </summary>
public static class SwaggerAccesoCalculos
{
    /// <summary>Minutos de inactividad tras los cuales la sesión de Swagger vence.</summary>
    public const int MinutosInactividad = 6;

    /// <summary>Separador entre el vencimiento y su firma dentro de la cookie.</summary>
    private const char SeparadorCookie = '.';

    /// <summary>
    /// Rutas que el gate protege. Se evalúa sobre el path en minúsculas.
    /// <c>/swagger-ui</c> ya cae dentro de <c>/swagger</c>; se nombra igual porque es una ruta
    /// propia (el CSS oscuro) y no un detalle de Swashbuckle.
    /// </summary>
    public static bool EsRutaProtegida(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var p = path.ToLowerInvariant();
        return p.StartsWith("/swagger") || p.StartsWith("/swagger-ui");
    }

    /// <summary>
    /// Única ruta exenta: el POST del propio formulario de acceso. Si el gate lo interceptara,
    /// no habría forma de autenticarse nunca.
    /// </summary>
    public static bool EsRutaExenta(string? metodoHttp, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (!string.Equals(metodoHttp, "POST", StringComparison.OrdinalIgnoreCase)) return false;
        return path.ToLowerInvariant().Contains("/swagger/login");
    }

    /// <summary>
    /// Compara la contraseña en <b>tiempo fijo</b>: la comparación con <c>==</c> corta en el primer
    /// byte distinto y filtra el prefijo correcto en el tiempo de respuesta.
    ///
    /// <para>
    /// <b>Fail-closed:</b> si el ambiente no configuró contraseña, no entra nadie. Antes había una
    /// constante hardcodeada como respaldo en dos archivos, así que vaciar la configuración no
    /// cerraba la puerta: la abría con una contraseña que está en el repositorio.
    /// </para>
    /// </summary>
    public static bool PasswordCorrecta(string? recibida, string? esperada)
    {
        if (string.IsNullOrEmpty(esperada)) return false;
        if (string.IsNullOrEmpty(recibida)) return false;

        var a = Encoding.UTF8.GetBytes(recibida);
        var b = Encoding.UTF8.GetBytes(esperada);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>
    /// Emite el valor de la cookie de sesión: <c>&lt;vencimientoUtcTicks&gt;.&lt;firma&gt;</c>.
    ///
    /// <para>
    /// El vencimiento va <b>dentro</b> de lo firmado. Antes la vigencia vivía en una segunda cookie
    /// (<c>*_LastActivity</c>) que el cliente podía reescribir: el timeout de 6 minutos era
    /// decorativo, porque bastaba con mandar una marca de tiempo nueva para renovarse solo.
    /// </para>
    /// </summary>
    public static string EmitirCookie(string passwordEsperada, string? ip, DateTime ahoraUtc)
    {
        var vencimiento = ahoraUtc.AddMinutes(MinutosInactividad).Ticks;
        return $"{vencimiento}{SeparadorCookie}{Firmar(vencimiento, passwordEsperada, ip)}";
    }

    /// <summary>
    /// ¿La cookie es auténtica y todavía está vigente? Fail-closed ante cualquier duda: formato
    /// raro, firma que no coincide, vencimiento ilegible o ya pasado.
    ///
    /// <para>
    /// La firma ata la sesión a la IP de origen igual que antes, para que una cookie copiada a otra
    /// máquina no sirva.
    /// </para>
    /// </summary>
    public static bool CookieVigente(string? valorCookie, string? passwordEsperada, string? ip, DateTime ahoraUtc)
    {
        if (string.IsNullOrWhiteSpace(valorCookie)) return false;
        if (string.IsNullOrEmpty(passwordEsperada)) return false;

        var partes = valorCookie.Split(SeparadorCookie);
        if (partes.Length != 2) return false;

        if (!long.TryParse(partes[0], out var vencimientoTicks)) return false;

        var firmaEsperada = Firmar(vencimientoTicks, passwordEsperada, ip);
        var a = Encoding.UTF8.GetBytes(partes[1]);
        var b = Encoding.UTF8.GetBytes(firmaEsperada);
        if (!CryptographicOperations.FixedTimeEquals(a, b)) return false;

        return vencimientoTicks > ahoraUtc.Ticks;
    }

    /// <summary>
    /// HMAC-SHA256 del vencimiento. La clave sale de la contraseña del ambiente más la IP: sin
    /// conocer la contraseña no se puede fabricar una cookie, y la de otra IP no vale.
    /// </summary>
    private static string Firmar(long vencimientoTicks, string passwordEsperada, string? ip)
    {
        var clave = Encoding.UTF8.GetBytes($"{passwordEsperada}|{ip ?? ""}");
        var mensaje = Encoding.UTF8.GetBytes(vencimientoTicks.ToString());
        using var hmac = new HMACSHA256(clave);
        return Convert.ToBase64String(hmac.ComputeHash(mensaje));
    }
}
