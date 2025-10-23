// src/ZooSanMarino.Application/Options/JwtOptions.cs
namespace ZooSanMarino.Application.Options;

public sealed class JwtOptions
{
    /// <summary>Clave simétrica usada para firmar el JWT (mín. 16 chars recomendado).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Issuer/emisor del token.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Audience/audiencia del token.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Duración del token en minutos.</summary>
    public int DurationInMinutes { get; set; } = 120;

    /// <summary>Valida que las opciones mínimas estén presentes.</summary>
    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(Key) || Key.Length < 16)
            throw new InvalidOperationException("JwtOptions.Key no configurado o demasiado corto (>= 16).");
        if (string.IsNullOrWhiteSpace(Issuer))
            throw new InvalidOperationException("JwtOptions.Issuer no configurado.");
        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("JwtOptions.Audience no configurado.");
        if (DurationInMinutes <= 0)
            throw new InvalidOperationException("JwtOptions.DurationInMinutes debe ser > 0.");
    }
}
