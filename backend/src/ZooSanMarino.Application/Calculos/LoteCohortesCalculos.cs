namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculos puros (sin EF ni estado) de las COHORTES de aves de un lote y de la habilitación del
/// traslado CROSS-ETAPA (Levante → Producción).
/// <para>
/// Una cohorte es un grupo de aves que ingresó a un lote por traslado conservando la edad que traía
/// de su lote de origen: su edad se cuenta SIEMPRE desde la <c>fecha_encaset</c> del lote ORIGEN
/// (<c>fecha_encaset_cohorte</c>), no desde la fecha de ingreso ni desde el encaset del receptor.
/// </para>
/// La fórmula de semanas NO se duplica: delega en
/// <see cref="MovimientoAvesCalculos.SemanaDesdeEncaset(DateTime, DateTime)"/> (división entera por
/// 7 + 1 ⇒ el día del encasetamiento es la semana 1).
/// </summary>
public static class LoteCohortesCalculos
{
    /// <summary>Etapa "Levante" tal como viaja en los DTOs de traslado.</summary>
    public const string EtapaLevante = "Levante";

    /// <summary>Etapa "Produccion" tal como viaja en los DTOs de traslado.</summary>
    public const string EtapaProduccion = "Produccion";

    /// <summary>
    /// Edad en DÍAS de la cohorte a la <paramref name="fecha"/> indicada, contada desde
    /// <paramref name="fechaEncaset"/>. Día del encasetamiento = 0. Se aplica <b>clamp a 0</b>
    /// cuando la fecha consultada es anterior al encasetamiento (edad negativa carece de sentido).
    /// </summary>
    public static int EdadDias(DateOnly fechaEncaset, DateOnly fecha)
    {
        var dias = fecha.DayNumber - fechaEncaset.DayNumber;
        return dias > 0 ? dias : 0;
    }

    /// <summary>
    /// Edad en SEMANAS (1-based) de la cohorte a la <paramref name="fecha"/> indicada: el día del
    /// encasetamiento es la semana 1. Usa la misma aritmética que el resto del sistema
    /// (<see cref="MovimientoAvesCalculos.SemanaDesdeEncaset(DateTime, DateTime)"/>) sobre los días
    /// ya clampados por <see cref="EdadDias"/>.
    /// </summary>
    public static int EdadSemanas(DateOnly fechaEncaset, DateOnly fecha)
    {
        var encaset = fechaEncaset.ToDateTime(TimeOnly.MinValue);
        var fechaClampada = encaset.AddDays(EdadDias(fechaEncaset, fecha));
        return MovimientoAvesCalculos.SemanaDesdeEncaset(fechaClampada, encaset);
    }

    /// <summary>
    /// <c>true</c> si origen y destino son la MISMA etapa (comparación case-insensitive, idéntica a
    /// la que hacía el servicio de traslados inline).
    /// </summary>
    public static bool EsMismaEtapa(string? tipoOrigen, string? tipoDestino) =>
        string.Equals(tipoOrigen, tipoDestino, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>true</c> cuando la etapa indicada es Levante. Cualquier otro valor (incluye null) se
    /// considera Producción — misma discriminación que usa el servicio de traslados.
    /// </summary>
    public static bool EsLevante(string? tipo) =>
        string.Equals(tipo, EtapaLevante, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// ¿Se puede ejecutar el traslado con estas etapas?
    /// <list type="bullet">
    /// <item>Misma etapa → SIEMPRE permitido (comportamiento histórico intacto).</item>
    /// <item>Etapas distintas → solo si la empresa lo permite (<paramref name="companyPermite"/>)
    /// y EXCLUSIVAMENTE en el sentido Levante → Producción.</item>
    /// <item>Producción → Levante → NUNCA (ni con el flag activo).</item>
    /// </list>
    /// </summary>
    public static bool PuedeTrasladarCrossEtapa(bool companyPermite, string? tipoOrigen, string? tipoDestino)
    {
        if (EsMismaEtapa(tipoOrigen, tipoDestino)) return true;
        if (!companyPermite) return false;
        return EsLevante(tipoOrigen) && !EsLevante(tipoDestino);
    }

    /// <summary>
    /// Mensaje EXACTO del bloqueo cross-etapa (se conserva textual: el front y los smokes lo
    /// muestran tal cual desde que existe el traslado desde seguimiento diario).
    /// </summary>
    public static string MensajeCrossEtapaBloqueado(string? tipoOrigen, string? tipoDestino) =>
        $"No se permite cross-phase: origen={tipoOrigen} no coincide con destino={tipoDestino}. " +
        "Sólo se puede trasladar dentro de la misma etapa (Levante→Levante o Producción→Producción).";
}
