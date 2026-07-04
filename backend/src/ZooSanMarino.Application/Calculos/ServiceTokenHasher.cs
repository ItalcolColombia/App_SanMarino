using System.Security.Cryptography;
using System.Text;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica PURA de tokens de servicio (PAT): generación, hashing y verificación.
/// Sin EF ni estado → testeable en aislamiento. El service la reutiliza.
/// </summary>
public static class ServiceTokenHasher
{
    /// <summary>Prefijo que identifica un token de servicio (usado por el policy scheme "Smart").</summary>
    public const string Prefix = "sk_";

    /// <summary>Cantidad de bytes aleatorios (CSPRNG) del secreto (32 → 43 chars Base64Url).</summary>
    private const int SecretBytes = 32;

    /// <summary>
    /// Genera un token plano nuevo: "sk_" + Base64Url(32 bytes de RandomNumberGenerator).
    /// El plano NUNCA se persiste; se muestra una única vez al emitir.
    /// </summary>
    public static string GenerateToken()
    {
        var buffer = new byte[SecretBytes];
        RandomNumberGenerator.Fill(buffer);
        return Prefix + Base64UrlEncode(buffer);
    }

    /// <summary>SHA-256 del token plano, en hex minúscula. Determinístico.</summary>
    public static string Hash(string plainToken)
    {
        ArgumentNullException.ThrowIfNull(plainToken);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Verifica un token plano contra un hash almacenado, en tiempo constante
    /// (evita timing attacks). Devuelve false ante nulls/vacíos.
    /// </summary>
    public static bool Verify(string? plainToken, string? expectedHash)
    {
        if (string.IsNullOrEmpty(plainToken) || string.IsNullOrEmpty(expectedHash))
            return false;

        var actual = Encoding.UTF8.GetBytes(Hash(plainToken));
        var expected = Encoding.UTF8.GetBytes(expectedHash.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
