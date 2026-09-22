// src/ZooSanMarino.Application/Calculos/JwtRotacionClaveCalculos.cs
// Regla PURA de con que claves se VALIDAN los JWT cuando la clave rota. Sin configuracion, sin I/O.
using System.Text;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Claves con las que se aceptan los JWT entrantes. Se <b>firma</b> siempre con la clave actual; se
/// <b>valida</b> con la actual y, si sirve, con la anterior.
///
/// <para>
/// <b>Por qué existe.</b> En producción el deploy rota la clave en cada despliegue editando la
/// TaskDef (<c>backend/scripts/rotar-clave-jwt-taskdef.js</c>): la que había pasa a
/// <c>JwtSettings__PreviousKey</c> y <c>JwtSettings__Key</c> recibe una nueva. Sin la anterior, cada
/// deploy dejaría afuera a todos los usuarios con sesión abierta; con ella, los tokens emitidos antes
/// del deploy siguen valiendo hasta vencer y la sesión sigue atada a <c>sesiones_activas</c> igual.
/// </para>
///
/// <para>
/// Una anterior que no sirve (corta, igual a la actual o —en producción— una clave del repo) se
/// <b>ignora</b> en vez de impedir el arranque: nadie del equipo administra AWS, así que una TaskDef
/// rara no puede dejar la API caída. Ignorarla es lo seguro: esos tokens simplemente no valen.
/// </para>
/// </summary>
public static class JwtRotacionClaveCalculos
{
    /// <summary>Mismo mínimo que exige <c>JwtOptions.EnsureValid</c> para la clave actual.</summary>
    private const int MinimoBytes = 32;

    /// <summary>La clave actual primero y, si sirve, la anterior.</summary>
    public static IReadOnlyList<string> ClavesDeValidacion(string clave, string? claveAnterior, bool esProduccion)
    {
        if (string.IsNullOrWhiteSpace(claveAnterior)
            || string.Equals(claveAnterior, clave, StringComparison.Ordinal)
            || Encoding.UTF8.GetByteCount(claveAnterior) < MinimoBytes
            || (esProduccion && JwtClaveProduccionCalculos.MotivoRechazo(claveAnterior) is not null))
            return [clave];

        return [clave, claveAnterior];
    }
}
