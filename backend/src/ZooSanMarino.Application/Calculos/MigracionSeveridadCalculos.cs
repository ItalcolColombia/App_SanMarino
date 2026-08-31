using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Qué cuenta como error en una importación masiva, y qué es solo un aviso.
///
/// <para>
/// 🔴 <b>Por qué existe.</b> Errores y advertencias viven en la MISMA lista: <c>MigracionErrorDto</c>
/// nace con <c>Severidad = "Error"</c> y las informativas la pasan explícita. Contar
/// <c>errores.Count</c> a secas hacía que una simple <b>Advertencia descartara el día completo</b>, y
/// el resumen igual reportaba «Procesado» porque el badge mira otra cuenta. Ese fue el mecanismo
/// genérico detrás de «la carga llegó hasta la semana N» —el caso del lote S369, que quedó clavado en
/// 24 semanas—. La regla vive acá, y no repetida en cada bloque, para que no vuelva a divergir entre
/// levante, producción y engorde.
/// </para>
/// </summary>
public static class MigracionSeveridadCalculos
{
    /// <summary>Severidad de lo que invalida una fila. El resto es informativo.</summary>
    public const string Error = "Error";

    /// <summary>Severidad de un aviso: se muestra, pero <b>no</b> descarta nada.</summary>
    public const string Advertencia = "Advertencia";

    /// <summary>Un aviso no invalida la fila que lo produjo.</summary>
    public static bool DescartaLaFila(MigracionErrorDto e) =>
        string.Equals(e.Severidad, Error, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Cuántos de los acumulados descartan su fila. Se compara contra la marca tomada antes de leer
    /// la fila: si subió, esa fila no entra.
    /// </summary>
    public static int CuentaQueDescartan(IEnumerable<MigracionErrorDto> errores) =>
        errores.Count(DescartaLaFila);

    /// <summary>
    /// Filas distintas con al menos un error propio. Las de <c>Fila = 0</c> quedan afuera a
    /// propósito: no son de una fila sino <b>del archivo entero</b> (por ejemplo, que el consumo no
    /// alcanza contra el stock de la granja).
    ///
    /// <para>
    /// ⚠️ Que den 0 <b>no</b> significa que el archivo esté bien: puede estar rechazado entero por un
    /// error global. Quien decida si se puede reintentar una importación parcial tiene que mirar
    /// también <see cref="HayErroresDelArchivo"/> — si los hay, no hay nada que saltear y el usuario
    /// necesita saber qué corregir antes de volver a subirlo.
    /// </para>
    /// </summary>
    public static int FilasConError(IEnumerable<MigracionErrorDto> errores) =>
        errores.Where(e => DescartaLaFila(e) && e.Fila > 0).Select(e => e.Fila).Distinct().Count();

    /// <summary>
    /// Hay algún error que invalida el archivo completo (<c>Fila = 0</c>), y por lo tanto ninguna fila
    /// entra por más que se pida importación parcial.
    /// </summary>
    public static bool HayErroresDelArchivo(IEnumerable<MigracionErrorDto> errores) =>
        errores.Any(e => DescartaLaFila(e) && e.Fila <= 0);
}
