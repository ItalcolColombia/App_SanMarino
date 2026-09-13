namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Regla PURA de quién puede corregir la <b>FECHA / HORA de encasetamiento</b> de un lote que ya tiene
/// registros (plan <c>fase_de_desarrollo/fecha_encaset_recalculo_cascada_plan.md</c> §4.1).
///
/// <para>
/// <b>Por qué una key propia y no <c>lote.corregir_aves</c>.</b> Son dos correcciones distintas y el
/// usuario pidió poder darlas por separado: una mueve CUÁNTAS aves entraron, la otra mueve CUÁNDO
/// entraron. La fecha arrastra una cascada que las aves no tocan —re-fecha el cruce reproductora →
/// pollo engorde, corre el primer día con registro, mueve la ventana de alimento previo al encaset y
/// reescribe el saldo de toda la serie—, así que merece su propio permiso.
/// </para>
///
/// <para>
/// <b>Por qué el gate mira el DELTA y no el verbo.</b> El mismo <c>PUT</c> guarda el técnico, la
/// regional, el código ERP y la fecha. Pedir el permiso para todo el <c>PUT</c> lo convertiría en un
/// segundo <c>editar_registro</c> —el problema que <c>lote.corregir_aves</c> vino a resolver— y le
/// rompería la pantalla a quien solo venía a corregir un nombre.
/// </para>
///
/// <para>
/// <b>Y por qué además mira si el lote tiene registros.</b> Un lote recién creado al que se le
/// corrige la fecha el mismo día no está "corrigiendo el histórico": no hay ningún seguimiento que
/// recalcular ni ninguna serie que mover. Exigir el permiso ahí solo trabaría el alta.
/// </para>
/// </summary>
public static class CorreccionFechaEncasetAutorizacionCalculos
{
    /// <summary>Permiso que habilita corregir la fecha/hora de encasetamiento de un lote con registros.</summary>
    public const string PermisoCorregirFechaEncaset = "lote.corregir_fecha_encaset";

    /// <summary>
    /// Mensaje del rechazo. Nombra la acción concreta —no «no tiene permisos»— para que la persona
    /// sepa qué pedirle a quien administra los roles.
    /// </summary>
    public const string MensajeSinPermiso =
        "No tiene permiso para corregir la fecha u hora de encasetamiento de un lote que ya tiene registros. " +
        "El resto de los datos del lote sí se pueden editar.";

    /// <summary>
    /// ¿Este usuario puede guardar este cambio de fecha/hora?
    /// <para>
    /// Fail-closed: una lista de permisos nula equivale a no tener ninguno.
    /// </para>
    /// </summary>
    /// <param name="cambiaFechaOHora">El <c>PUT</c> trae una fecha o una hora distinta de la guardada.</param>
    /// <param name="loteConRegistros">
    /// El lote ya tiene seguimiento cargado — propio o de sus lotes reproductora. Sin registros no hay
    /// nada que recalcular y la corrección no necesita permiso.
    /// </param>
    public static bool PuedeAplicar(bool cambiaFechaOHora, bool loteConRegistros, IEnumerable<string>? permisos)
    {
        if (!cambiaFechaOHora || !loteConRegistros) return true;
        return TienePermiso(permisos);
    }

    /// <summary>
    /// ¿La lista de permisos incluye la key? Comparación <b>ordinal</b>, igual que los
    /// <c>_current.Permissions.Contains(...)</c> que ya usan los controllers del repo.
    /// </summary>
    public static bool TienePermiso(IEnumerable<string>? permisos) =>
        permisos is not null && permisos.Contains(PermisoCorregirFechaEncaset, StringComparer.Ordinal);
}
