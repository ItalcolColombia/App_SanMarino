// src/ZooSanMarino.Application/Calculos/PlatformSecretCalculos.cs
// Reglas PURAS de la firma de plataforma (`X-Secret-Up`). Sin HttpContext, sin config, sin I/O.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// La firma de plataforma (<c>X-Secret-Up</c>) es un <b>filtro de origen</b>, no autenticación: corre
/// antes de <c>UseAuthentication</c>, sin tocar la base, y frena tráfico automatizado. Hasta sept-2026
/// su valor era un secreto estático embebido en el bundle del frontend (extraíble por cualquiera).
///
/// <para>
/// Desde este cambio, el frontend web manda una firma <b>por sesión</b>:
/// <c>Base64(HMAC-SHA256(DerivationKey, jti))</c>, donde <c>jti</c> es el identificador de sesión que
/// B1 ya emite (<c>AuthService.GenerateResponseAsync</c>) y valida en cada request contra
/// <c>sesiones_activas</c> (<see cref="RevocacionSesionCalculos"/>). La firma hereda gratis vida de
/// sesión, expiración y revocación. La <c>DerivationKey</c> vive <b>solo</b> en el servidor.
/// </para>
///
/// <para>
/// El middleware lee el <c>jti</c> parseando el payload del JWT <b>sin validar la firma</b> — la
/// validación real (firma, <c>exp</c>, B1) la hace <c>OnTokenValidated</c> unos ms después. Es seguro:
/// forjar una firma válida para un <c>jti</c> arbitrario exige la <c>DerivationKey</c>, y un JWT con
/// firma falsa igual muere en <c>OnTokenValidated</c>.
/// </para>
/// </summary>
public static class PlatformSecretCalculos
{
    /// <summary>
    /// <c>Base64(HMAC-SHA256(derivationKey, jti))</c>. <c>null</c> si falta cualquiera de los dos
    /// insumos — el llamador cae al camino legacy y nada se rompe.
    /// </summary>
    public static string? DerivarClaveSesion(string? jti, string? derivationKey)
    {
        if (string.IsNullOrWhiteSpace(jti) || string.IsNullOrWhiteSpace(derivationKey))
            return null;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(derivationKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(jti));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Comparación en <b>tiempo constante</b>. <c>null</c> en cualquiera ⇒ <c>false</c>. Largos
    /// distintos ⇒ <c>false</c> sin excepción (sólo filtra por largo, que acá es fijo: Base64 de
    /// SHA-256 = 44 caracteres).
    /// </summary>
    public static bool FirmasCoinciden(string? a, string? b)
    {
        if (a is null || b is null) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));
    }

    /// <summary>
    /// Extrae el claim <c>jti</c> del <c>Authorization: Bearer &lt;jwt&gt;</c> <b>sin validar la
    /// firma</b>. Fail-safe: header ausente, esquema distinto, PAT (<c>sk_…</c>), JWT malformado,
    /// base64 roto o payload sin <c>jti</c> ⇒ <c>null</c>. Nunca lanza.
    /// </summary>
    public static string? LeerJtiDeAuthorizationHeader(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader)) return null;

        var header = authorizationHeader.Trim();
        const string prefijo = "Bearer ";
        if (!header.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase)) return null;

        var token = header[prefijo.Length..].Trim();
        // Los PAT de servicio no son JWT y no tienen `jti`.
        if (token.Length == 0 || token.StartsWith("sk_", StringComparison.OrdinalIgnoreCase))
            return null;

        var partes = token.Split('.');
        if (partes.Length != 3) return null;

        try
        {
            var payloadJson = DecodificarBase64Url(partes[1]);
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("jti", out var jtiProp)) return null;

            var jti = jtiProp.ValueKind == JsonValueKind.String
                ? jtiProp.GetString()
                : jtiProp.ToString();

            return string.IsNullOrWhiteSpace(jti) ? null : jti;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Base64URL (RFC 7515): <c>-_</c> por <c>+/</c>, sin padding.</summary>
    private static string DecodificarBase64Url(string valor)
    {
        var s = valor.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}
