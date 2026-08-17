namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Clasificación de un ítem de cronograma <b>todavía sin aplicar</b> frente al día de hoy, para la
/// bandeja "hoy me toca": vencido, en franja o próximo dentro del horizonte.
///
/// <para>⚠ La consulta que alimenta la bandeja es <c>fn_vacunacion_pendientes</c>: <b>la SQL es la
/// dueña del número</b> (filtrar en memoria haría inviable el multipaís) y este cálculo puro es su
/// <b>especificación ejecutable</b> — la misma relación que <c>SeguimientoAvesEngordeCalculos</c>
/// tiene con su función. Si una de las dos cambia, el smoke que las compara fila a fila falla.</para>
/// </summary>
public static class VacunacionPendientesCalculos
{
    /// <summary>La franja ya venció y nadie registró nada.</summary>
    public const string SituacionVencido = "Vencido";

    /// <summary>Hoy cae dentro de la franja: es el momento de aplicar.</summary>
    public const string SituacionEnFranja = "EnFranja";

    /// <summary>Todavía no abre, pero abre dentro del horizonte pedido.</summary>
    public const string SituacionProximo = "Proximo";

    /// <summary>Horizonte por defecto de "lo que viene": una semana.</summary>
    public const int DiasHorizontePorDefecto = 7;

    /// <summary>
    /// Situación + días de desviación respecto de hoy. Positivo = días de atraso (la franja cerró
    /// hace tanto); cero = hoy está dentro; negativo = faltan tantos días para que abra.
    /// </summary>
    public readonly record struct Clasificacion(string Situacion, int Dias);

    /// <summary>
    /// Clasifica una franja contra <paramref name="hoy"/>. Devuelve <c>null</c> cuando la fila
    /// <b>no</b> pertenece a la bandeja: abre más allá del horizonte pedido.
    /// </summary>
    /// <remarks>
    /// El día del fin de franja <b>todavía cumple</b> (<c>hoy == fin</c> ⇒ en franja, no vencido):
    /// es la misma frontera que usa <see cref="VacunacionCalculos.ProyectarAplicacion"/> para no
    /// exigir motivo. Un horizonte negativo se trata como 0 (no hay "próximos").
    /// </remarks>
    public static Clasificacion? Clasificar(
        DateTime inicioFranja, DateTime finFranja, DateTime hoy, int diasHorizonte)
    {
        var inicio = inicioFranja.Date;
        var fin = finFranja.Date;
        var dia = hoy.Date;

        if (fin < dia)
            return new Clasificacion(SituacionVencido, (dia - fin).Days);

        if (inicio <= dia)
            return new Clasificacion(SituacionEnFranja, 0);

        var faltan = (inicio - dia).Days;
        if (faltan > Math.Max(diasHorizonte, 0)) return null;

        return new Clasificacion(SituacionProximo, -faltan);
    }
}
