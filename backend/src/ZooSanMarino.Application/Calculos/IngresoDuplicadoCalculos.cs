namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Detecta que un ingreso de alimento repite uno ya cargado, para avisarlo antes de duplicar el
/// stock de un galpón.
///
/// <para>
/// 🔴 <b>Por qué existe.</b> <c>RegistrarIngresoAsync</c> valida cantidad, ítem, empresa, silo y
/// coherencia de origen, pero <b>no consulta si ese ingreso ya está</b>: dos cargas del mismo remito
/// suman kilos que nunca entraron. Pasó en producción y el caso se cerró corrigiendo los datos, sin
/// tocar el camino que lo permite — así que puede volver a pasar hoy. La casa ya sabía hacerlo: la
/// carga masiva tiene idempotencia (<c>MigracionAlimentoCalculos.ClaveIdempotencia</c>), pero esa
/// protección vive <b>solo</b> en el camino del Excel.
/// </para>
///
/// <para>
/// <b>Aviso confirmable, no bloqueo, y sin índice único.</b> Repetir una carga es raro pero legítimo
/// —una entrega parcial que llega en dos viajes el mismo día—, y hay llamadores internos que repiten
/// clave a propósito (las devoluciones automáticas por eliminación de seguimiento). Un <c>UNIQUE</c>
/// en la tabla los rompería a todos. El usuario ve el aviso, decide, y reenvía con
/// <c>ConfirmarDuplicado</c>.
/// </para>
///
/// <para>
/// <b>La remisión es lo que hace único a un ingreso</b>, no la cantidad ni el día: dos camiones del
/// mismo alimento el mismo día son dos ingresos reales y no deben avisar nada. Por eso un ingreso
/// <b>sin</b> referencia nunca se considera duplicado — no hay con qué afirmarlo.
/// </para>
/// </summary>
public static class IngresoDuplicadoCalculos
{
    /// <summary>
    /// ¿Vale la pena buscar un duplicado de este ingreso? Solo si trae remisión y una cantidad real.
    /// Las devoluciones automáticas se excluyen: repiten clave por diseño.
    /// </summary>
    public static bool AmeritaChequeo(string? referencia, decimal cantidad, bool confirmadoPorElUsuario)
    {
        if (confirmadoPorElUsuario) return false;
        if (cantidad <= 0) return false;

        var r = (referencia ?? "").Trim();
        if (r.Length == 0) return false;

        return !EsReferenciaDeSistema(r);
    }

    /// <summary>
    /// Referencias que escribe el propio sistema y que repiten clave a propósito: no son cargas del
    /// usuario y avisarlas sería ruido.
    /// </summary>
    public static bool EsReferenciaDeSistema(string referencia) =>
        referencia.Contains("devoluc", StringComparison.OrdinalIgnoreCase) ||
        referencia.Contains("(validado)", StringComparison.OrdinalIgnoreCase) ||
        referencia.StartsWith("Seguimiento ", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Mensaje del aviso. Nombra la remisión y el movimiento que ya existe, para que el usuario
    /// pueda ir a mirarlo antes de decidir si de verdad es una segunda entrega.
    /// </summary>
    public static string MensajeDuplicado(string? referencia, decimal cantidad, string? unidad, int movimientoExistenteId) =>
        $"Ya hay un ingreso registrado con la remisión «{(referencia ?? "").Trim()}» " +
        $"por {cantidad:0.###} {(string.IsNullOrWhiteSpace(unidad) ? "kg" : unidad.Trim())} " +
        $"en esta misma ubicación (movimiento #{movimientoExistenteId}). " +
        "Si es una segunda entrega con la misma remisión, confirmá para registrarlo igual; " +
        "si no, revisá el movimiento existente antes de volver a cargarlo.";
}
