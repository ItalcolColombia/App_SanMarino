// src/ZooSanMarino.Application/Calculos/EncasetamientoCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo PURO del <b>primer día con registro</b> de un lote a partir de la hora en que llegaron las
/// aves. Aplica a pollo engorde y a reproductora aves de engorde.
/// <para>
/// Regla de negocio: si las aves llegan <b>antes de las 13:00</b>, alcanzan a consumir ese mismo día y
/// el primer registro va en la fecha de encasetamiento. Si llegan <b>a las 13:00 o después</b>, el
/// primer consumo real es al día siguiente.
/// </para>
/// <para>
/// La <b>fecha de encasetamiento no cambia nunca</b> (es el día real de llegada) y la <b>edad se sigue
/// contando desde ella</b>: un lote tardío simplemente arranca en edad 1 (Día 2). Por eso este cálculo
/// no toca la aritmética de edad/semana, ni la guía genética, ni los indicadores — solo decide desde
/// qué día se puede capturar.
/// </para>
/// <para>
/// La hora es OPCIONAL: sin hora (todos los lotes anteriores a esta funcionalidad) el resultado es
/// idéntico al comportamiento previo, así que no hace falta backfill.
/// </para>
/// </summary>
public static class EncasetamientoCalculos
{
    /// <summary>
    /// Hora de corte, INCLUSIVE: 13:00 en punto ya cuenta como llegada tardía. La franja del mediodía
    /// (12:00–12:59) queda del lado temprano.
    /// </summary>
    public static readonly TimeOnly HoraCorte = new(13, 0);

    /// <summary>
    /// Hora que realmente aplica según el flag de la empresa. Con la regla APAGADA devuelve
    /// <c>null</c>, y como todo el cálculo trata "sin hora" igual que antes, el comportamiento de esa
    /// empresa queda idéntico al previo aunque alguien haya cargado una hora en el lote.
    /// </summary>
    public static TimeOnly? HoraEfectiva(TimeOnly? horaEncasetamiento, bool reglaActiva) =>
        reglaActiva ? horaEncasetamiento : null;

    /// <summary>
    /// <c>true</c> si las aves llegaron tan tarde que su primer consumo cae al día siguiente.
    /// Sin hora informada ⇒ <c>false</c> (comportamiento previo).
    /// </summary>
    public static bool LlegadaTardia(TimeOnly? horaEncasetamiento) =>
        horaEncasetamiento.HasValue && horaEncasetamiento.Value >= HoraCorte;

    /// <summary>Días que se corre el primer registro respecto del encasetamiento: 0 o 1.</summary>
    public static int DiasDesplazamiento(TimeOnly? horaEncasetamiento) =>
        LlegadaTardia(horaEncasetamiento) ? 1 : 0;

    /// <summary>
    /// Primera fecha en la que el lote admite un registro de seguimiento. Conserva el <c>Kind</c> y la
    /// hora del <paramref name="fechaEncasetamiento"/> recibido (las fechas puras del sistema vienen
    /// ancladas a mediodía UTC), así que se puede comparar directo contra otras fechas del dominio.
    /// </summary>
    public static DateTime PrimerDiaConRegistro(DateTime fechaEncasetamiento, TimeOnly? horaEncasetamiento) =>
        fechaEncasetamiento.AddDays(DiasDesplazamiento(horaEncasetamiento));

    /// <summary>
    /// Edad (días desde el encasetamiento) del primer registro válido: 0 si llegaron temprano, 1 si
    /// llegaron a las 13:00 o después.
    /// </summary>
    public static int EdadMinimaConRegistro(TimeOnly? horaEncasetamiento) =>
        DiasDesplazamiento(horaEncasetamiento);

    /// <summary>
    /// Texto para el mensaje de error de captura, para que el usuario entienda que la restricción sale
    /// de la hora de llegada y no de un bug. Devuelve <c>null</c> si el lote no es tardío.
    /// </summary>
    public static string? MotivoDesplazamiento(TimeOnly? horaEncasetamiento) =>
        LlegadaTardia(horaEncasetamiento)
            ? $"las aves llegaron a las {horaEncasetamiento!.Value:HH\\:mm} (desde las {HoraCorte:HH\\:mm} el primer consumo va al día siguiente)"
            : null;
}
