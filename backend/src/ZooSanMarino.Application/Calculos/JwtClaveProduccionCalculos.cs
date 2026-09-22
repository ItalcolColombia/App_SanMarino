// src/ZooSanMarino.Application/Calculos/JwtClaveProduccionCalculos.cs
// Regla PURA de si una clave de firma JWT sirve para PRODUCCION. Sin configuracion, sin I/O.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Decide si la clave con la que se firman los JWT puede usarse en <b>producción</b>.
///
/// <para>
/// <b>Por qué existe.</b> En ECS la clave real llega por la TaskDef (<c>JwtSettings__Key</c>), que pisa
/// a <c>appsettings.json</c>. Pero <c>appsettings.json</c> viaja dentro de la imagen y está versionado
/// en git con la clave de desarrollo: si la TaskDef no define la variable, .NET cae en esa clave
/// <b>sin avisar</b> y producción firma tokens con una clave que tiene cualquiera con acceso al repo.
/// Esta regla hace que ese caso <b>no arranque</b> (fail-closed) en vez de quedar mudo.
/// </para>
///
/// <para>
/// El largo mínimo (32 bytes) lo sigue validando <c>JwtOptions.EnsureValid</c>; acá solo se reconocen
/// las claves de ejemplo o desarrollo que viven en el repo.
/// </para>
/// </summary>
public static class JwtClaveProduccionCalculos
{
    /// <summary>
    /// Marcas de las claves no productivas del repo. Las que llevan <c>_</c> no pueden salir de una
    /// clave base64 estándar (<c>A-Z a-z 0-9 + / =</c>) y <c>Development</c> tiene 11 letras, así que
    /// una clave aleatoria no las contiene por casualidad.
    /// </summary>
    private static readonly string[] MarcasNoProductivas =
    [
        "Development", // appsettings.json ("...Development_Only...") y appsettings.Development.json.example
        "YOUR_",       // appsettings.json.example / appsettings.Development.json.example
        "REEMPLAZAR",  // placeholder en español de los ejemplos
        "CHANGE_ME",
    ];

    /// <summary>
    /// <c>null</c> si la clave sirve para producción; si no, el motivo para cortar el arranque.
    /// El mensaje <b>nunca</b> incluye la clave.
    /// </summary>
    public static string? MotivoRechazo(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
            return "JwtSettings:Key no esta configurada en produccion. La pone el deploy del pipeline en la " +
                   "TaskDef de ECS (JwtSettings__Key); ver backend/deploy/jwt-produccion.example.md.";

        foreach (var marca in MarcasNoProductivas)
        {
            if (clave.Contains(marca, StringComparison.OrdinalIgnoreCase))
                return $"JwtSettings:Key de produccion es una clave de ejemplo o de desarrollo del repositorio " +
                       $"(contiene \"{marca}\"). Falta JwtSettings__Key en la TaskDef de ECS: el deploy del " +
                       "pipeline la genera sola (ver backend/deploy/jwt-produccion.example.md).";
        }

        return null;
    }
}
