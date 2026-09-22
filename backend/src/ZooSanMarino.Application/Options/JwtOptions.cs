// src/ZooSanMarino.Application/Options/JwtOptions.cs
using System.Text;

namespace ZooSanMarino.Application.Options;

public sealed class JwtOptions
{
    /// <summary>Clave simétrica para HS256: al menos 32 bytes UTF-8.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Clave ANTERIOR: solo VALIDA, nunca firma. En producción llega de Secrets Manager
    /// (<c>AWSPREVIOUS</c>) para que rotar la clave en cada deploy no deje afuera los tokens ya
    /// emitidos. Opcional: vacía = solo se acepta <see cref="Key"/>.
    /// </summary>
    public string? PreviousKey { get; set; }

    /// <summary>Issuer/emisor del token.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Audience/audiencia del token.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Duración del token en minutos.</summary>
    public int DurationInMinutes { get; set; } = 120;

    /// <summary>Valida que las opciones mínimas estén presentes.</summary>
    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(Key) || Encoding.UTF8.GetByteCount(Key) < 32)
            throw new InvalidOperationException("JwtOptions.Key no configurado o demasiado corto (>= 32 bytes UTF-8).");
        if (!string.IsNullOrWhiteSpace(PreviousKey) && Encoding.UTF8.GetByteCount(PreviousKey) < 32)
            throw new InvalidOperationException("JwtOptions.PreviousKey demasiado corto (>= 32 bytes UTF-8).");
        if (string.IsNullOrWhiteSpace(Issuer))
            throw new InvalidOperationException("JwtOptions.Issuer no configurado.");
        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("JwtOptions.Audience no configurado.");
        if (DurationInMinutes <= 0)
            throw new InvalidOperationException("JwtOptions.DurationInMinutes debe ser > 0.");
    }
}
