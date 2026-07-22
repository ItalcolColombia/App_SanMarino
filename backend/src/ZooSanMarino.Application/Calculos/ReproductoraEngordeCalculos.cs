namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica pura del estado del lote reproductora aves de engorde.
/// Regla de negocio: el lote cierra SOLO cuando los días de recogida están CONFIRMADOS
/// (la confirmación es la que sincroniza cada registro hacia pollo engorde). Mientras haya
/// registros pendientes, el lote sigue "Vigente" — aunque se agoten las aves.
/// </summary>
public static class ReproductoraEngordeCalculos
{
    /// <summary>Días de recogida de datos del lote reproductora. Al confirmarlos todos, cierra.</summary>
    public const int DiasRecogidaReproductora = 7;

    /// <summary>
    /// Estado ("Cerrado"/"Vigente") + aves actuales del lote reproductora.
    /// <para>El cierre depende EXCLUSIVAMENTE de la cantidad de días CONFIRMADOS
    /// (<paramref name="numConfirmados"/>), no de los registrados. <paramref name="avesActuales"/>
    /// se conserva solo como saldo mostrado (no dispara el cierre).</para>
    /// </summary>
    public static (string Estado, int AvesActuales) CalcularEstado(
        int avesEncasetadas,
        int ventas,
        int mortalidad,
        int seleccion,
        int errorSexaje = 0,
        int numConfirmados = 0,
        int dias = DiasRecogidaReproductora)
    {
        var avesActuales = Math.Max(0, avesEncasetadas - mortalidad - seleccion - errorSexaje - ventas);
        var estado = numConfirmados >= dias ? "Cerrado" : "Vigente";
        return (estado, avesActuales);
    }

    /// <summary>
    /// Edad en días del lote reproductora.
    /// <para>Mientras está <b>Vigente</b> crece con el reloj: <c>hoy − fechaEncasetamiento</c> (comportamiento
    /// previo). Cuando el lote está <b>Cerrado</b> (7 días confirmados) la edad se <b>congela</b> en la
    /// <paramref name="fechaCierre"/> (fecha del último registro de recogida) para que no siga creciendo con
    /// la fecha del sistema.</para>
    /// </summary>
    /// <param name="fechaEncasetamiento">Fecha de encasetamiento del lote reproductora (null ⇒ edad 0).</param>
    /// <param name="hoyUtc">"Hoy" (UTC) — se pasa como parámetro para mantener la función pura/testeable.</param>
    /// <param name="cerrado">True si el lote está Cerrado (7 días confirmados).</param>
    /// <param name="fechaCierre">Fecha de cierre = MAX(fecha) de los registros de recogida. Solo se usa si <paramref name="cerrado"/>.</param>
    public static int CalcularEdadDias(DateTime? fechaEncasetamiento, DateTime hoyUtc, bool cerrado, DateTime? fechaCierre)
    {
        if (!fechaEncasetamiento.HasValue) return 0;
        var baseDate = fechaEncasetamiento.Value.Date;
        var asOf = (cerrado && fechaCierre.HasValue) ? fechaCierre.Value.Date : hoyUtc.Date;
        return Math.Max(0, (int)(asOf - baseDate).TotalDays);
    }
}
