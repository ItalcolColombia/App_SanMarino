namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Regla PURA de quién puede corregir el ENCASETAMIENTO de un lote que ya tiene seguimiento
/// (`fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md` §2).
///
/// <para>
/// <b>Por qué existe una key propia.</b> Hasta el 25-ago-2026 el único gate era
/// <c>editar_registro</c>, que es transversal: darlo para que alguien corrija las aves de un lote le
/// habilitaba al mismo tiempo editar filas del seguimiento diario, movimientos y ventas de pollo
/// engorde y movimientos de inventario. Y en POSTURA no había gate ninguno. Corregir el
/// encasetamiento no es una edición más: <b>reescribe toda la serie diaria</b> del lote, la
/// conversión, la mortalidad, los reportes y la liquidación, porque
/// <c>fn_seguimiento_diario_engorde</c> lee <c>aves_encasetadas</c> en vivo.
/// </para>
///
/// <para>
/// <b>Por qué el gate mira el DELTA y no el verbo.</b> El mismo <c>PUT</c> guarda el técnico, la
/// regional, el código ERP y las aves. Pedir el permiso para todo el <c>PUT</c> convertiría este
/// permiso en un segundo <c>editar_registro</c> —el problema que vino a resolver— y le rompería la
/// pantalla a quien solo venía a corregir un nombre. Solo se exige cuando el ajuste efectivamente
/// mueve aves.
/// </para>
/// </summary>
public static class CorreccionAvesLoteAutorizacionCalculos
{
    /// <summary>Permiso que habilita corregir el encasetamiento de un lote (engorde y postura).</summary>
    public const string PermisoCorregirAves = "lote.corregir_aves";

    /// <summary>
    /// Mensaje del rechazo. Nombra la acción concreta —no «no tiene permisos»— para que la persona
    /// sepa qué pedirle a quien administra los roles.
    /// </summary>
    public const string MensajeSinPermiso =
        "No tiene permiso para corregir las aves encasetadas de un lote. " +
        "El resto de los datos del lote sí se pueden editar.";

    /// <summary>
    /// ¿Este usuario puede aplicar un ajuste de encasetamiento?
    ///
    /// <para>
    /// <paramref name="ajusteMueveAves"/> es el resultado de
    /// <c>!AjusteEncasetamientoCalculos.SinCambio(delta)</c>: con delta cero la operación no es una
    /// corrección de aves y no necesita el permiso.
    /// </para>
    ///
    /// <para>
    /// Fail-closed: una lista de permisos nula equivale a no tener ninguno.
    /// </para>
    /// </summary>
    public static bool PuedeAplicar(bool ajusteMueveAves, IEnumerable<string>? permisos)
    {
        if (!ajusteMueveAves) return true;
        return TienePermiso(permisos);
    }

    /// <summary>
    /// ¿La lista de permisos incluye la key? Comparación <b>ordinal</b>, igual que los
    /// <c>_current.Permissions.Contains(...)</c> que ya usan los controllers del repo: una key es una
    /// key exacta, no una familia de capitalizaciones.
    /// </summary>
    public static bool TienePermiso(IEnumerable<string>? permisos) =>
        permisos is not null && permisos.Contains(PermisoCorregirAves, StringComparer.Ordinal);
}
