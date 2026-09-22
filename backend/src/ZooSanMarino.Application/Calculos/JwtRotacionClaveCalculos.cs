// src/ZooSanMarino.Application/Calculos/JwtRotacionClaveCalculos.cs
// Regla PURA de con que claves se VALIDAN los JWT cuando la clave rota. Sin configuracion, sin I/O.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Claves con las que se aceptan los JWT entrantes. Se <b>firma</b> siempre con la clave actual; se
/// <b>valida</b> con la actual y, si existe, con la anterior.
///
/// <para>
/// <b>Por qué existe.</b> En producción el pipeline genera una clave nueva en cada deploy
/// (Secrets Manager: <c>AWSCURRENT</c> → <c>JwtSettings__Key</c>, <c>AWSPREVIOUS</c> →
/// <c>JwtSettings__PreviousKey</c>). Sin la anterior, cada deploy dejaría afuera a todos los usuarios
/// con sesión abierta; con ella, los tokens emitidos antes del deploy siguen valiendo hasta vencer
/// (<c>JwtSettings__DurationInMinutes</c>) y la sesión sigue atada a <c>sesiones_activas</c> igual.
/// </para>
/// </summary>
public static class JwtRotacionClaveCalculos
{
    /// <summary>
    /// La clave actual primero y, si viene y es distinta, la anterior. El largo de ambas lo valida
    /// <c>JwtOptions.EnsureValid</c> antes de llegar acá.
    /// </summary>
    public static IReadOnlyList<string> ClavesDeValidacion(string clave, string? claveAnterior)
    {
        if (string.IsNullOrWhiteSpace(claveAnterior) || string.Equals(claveAnterior, clave, StringComparison.Ordinal))
            return [clave];

        return [clave, claveAnterior];
    }
}
