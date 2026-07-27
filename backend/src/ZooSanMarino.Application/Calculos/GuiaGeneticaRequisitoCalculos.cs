// Reglas PURAS de la exigencia de guía genética al crear/editar un lote (sin EF, sin estado).
//
// Regla de negocio (empresa nueva sin guía): mientras la empresa NO tenga ninguna fila de guía
// genética cargada, Raza y Año de tabla genética son OPCIONALES — se acepta raza como texto libre
// (o vacía) y año nulo, y se guardan tal cual (trim a la raza). Apenas la empresa carga su guía,
// vuelve a regir el comportamiento actual: ambos son requeridos y la combinación debe existir en
// la guía (la verificación de existencia la resuelve la BD desde el servicio).
namespace ZooSanMarino.Application.Calculos;

public static class GuiaGeneticaRequisitoCalculos
{
    /// <summary>Mensaje exacto cuando faltan Raza/Año y la empresa SÍ tiene guía cargada.</summary>
    public const string MensajeRequeridos =
        "Raza y Año de tabla genética son requeridos y deben existir en la guía genética cargada.";

    /// <summary>
    /// ¿Hay que exigir guía genética? Solo si la empresa tiene al menos una fila de guía cargada
    /// (<c>produccion_avicola_raw</c> viva para la empresa). Sin guía → raza/año opcionales.
    /// </summary>
    public static bool DebeExigirGuia(bool companyTieneGuia) => companyTieneGuia;

    /// <summary>
    /// Valida la selección de raza/año. Devuelve <c>null</c> si es válida y, si no, el mensaje de
    /// error. Sin guía cargada nunca hay error (raza libre/nula y año nulo son válidos).
    /// </summary>
    public static string? ValidarSeleccion(bool companyTieneGuia, string? raza, int? anoTablaGenetica)
    {
        if (!DebeExigirGuia(companyTieneGuia)) return null;

        if (string.IsNullOrWhiteSpace(raza) || !anoTablaGenetica.HasValue || anoTablaGenetica.Value <= 0)
            return MensajeRequeridos;

        return null;
    }

    /// <summary>
    /// ¿Hay que ir a la BD a verificar que la combinación (raza, año) exista en la guía? Solo cuando
    /// la empresa tiene guía y la selección pasó <see cref="ValidarSeleccion"/> (raza y año presentes).
    /// </summary>
    public static bool DebeVerificarExistenciaEnGuia(bool companyTieneGuia, string? raza, int? anoTablaGenetica)
        => DebeExigirGuia(companyTieneGuia)
           && ValidarSeleccion(companyTieneGuia, raza, anoTablaGenetica) is null;

    /// <summary>Mensaje exacto cuando la combinación (raza, año) no existe en la guía de la empresa.</summary>
    public static string MensajeGuiaInexistente(string? raza, int? anoTablaGenetica)
        => $"No existe guía genética para la raza '{raza}' y el año '{anoTablaGenetica}' en la compañía actual. Cargue la guía genética primero.";

    /// <summary>
    /// Raza a persistir: con guía cargada se guarda tal cual llega (comportamiento actual intacto);
    /// sin guía se guarda como texto libre normalizado (trim; vacío/espacios → <c>null</c>).
    /// </summary>
    public static string? ResolverRazaAGuardar(bool companyTieneGuia, string? raza)
    {
        if (DebeExigirGuia(companyTieneGuia)) return raza;

        var normalizada = (raza ?? string.Empty).Trim();
        return normalizada.Length == 0 ? null : normalizada;
    }
}
