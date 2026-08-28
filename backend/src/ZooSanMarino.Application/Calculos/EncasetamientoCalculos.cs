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
    /// Hora que aplica según el flag de empresa <c>primer_registro_segun_hora_llegada</c>. Con la
    /// regla APAGADA devuelve <c>null</c>, y como todo el cálculo trata "sin hora" igual que antes, el
    /// comportamiento de esa empresa queda idéntico al previo aunque el lote tenga hora cargada.
    /// <para>
    /// ⚠️ <b>Solo para el DÍA DE PESAJE</b> (<see cref="PesajeEngordeCalculos"/>). El PRIMER DÍA CON
    /// REGISTRO ya no se gatea por empresa: lo decide la hora del lote y punto (28-ago-2026). El
    /// formulario ofrece el campo "Hora de encasetamiento" con su leyenda a TODAS las empresas, y con
    /// el gate puesto ItalcolEcuador la llenó 16 veces —todas ≥ 13:00— sin que el backend la mirara:
    /// la pantalla prometía un comportamiento que no ocurría. Sin hora informada el desplazamiento es
    /// 0, así que las empresas que no usan el campo quedan byte a byte como antes.
    /// </para>
    /// <para>
    /// El pesaje SÍ conserva el gate: la guía genética de Ecuador está tabulada por días desde el
    /// encaset y correr el día de pesaje la desalinearía.
    /// </para>
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
    /// Número de DÍA de negocio de una fecha: el <b>primer día con registro del lote es el día 1</b>.
    /// <para>
    /// Es la numeración que el usuario cuenta y la que ya muestra reproductora. Se obtiene de la edad
    /// técnica descontando el desplazamiento por hora de llegada y sumando 1, así un lote que llegó
    /// tarde también arranca su semana en el día 1.
    /// </para>
    /// <para>
    /// Ojo: esto NO reemplaza a la edad. La edad (<c>fecha − fecha_encaset</c>) sigue siendo la que
    /// usan la guía genética, los indicadores, el informe semanal y la liquidación.
    /// </para>
    /// Devuelve 0 o negativo si la fecha es anterior al primer día con registro.
    /// </summary>
    public static int DiaDeNegocio(DateTime fecha, DateTime fechaEncasetamiento, TimeOnly? horaEncasetamiento) =>
        (int)(fecha.Date - fechaEncasetamiento.Date).TotalDays - DiasDesplazamiento(horaEncasetamiento) + 1;

    /// <summary>
    /// Semana de negocio a la que pertenece un día: los días 1..7 son la semana 1, 8..14 la 2, etc.
    /// Con desplazamiento 0 coincide exactamente con la semana que ya devuelve
    /// <c>fn_seguimiento_diario_engorde</c> (<c>ceil((edad+1)/7)</c>), así que no cambia nada para los
    /// lotes que no llegaron tarde. Días ≤ 0 (anteriores al primer registro) devuelven 0.
    /// </summary>
    public static int SemanaDeNegocio(int diaDeNegocio) =>
        diaDeNegocio <= 0 ? 0 : (diaDeNegocio + 6) / 7;

    /// <summary>
    /// Texto para el mensaje de error de captura, para que el usuario entienda que la restricción sale
    /// de la hora de llegada y no de un bug. Devuelve <c>null</c> si el lote no es tardío.
    /// </summary>
    public static string? MotivoDesplazamiento(TimeOnly? horaEncasetamiento) =>
        LlegadaTardia(horaEncasetamiento)
            ? $"las aves llegaron a las {horaEncasetamiento!.Value:HH\\:mm} (desde las {HoraCorte:HH\\:mm} el primer consumo va al día siguiente)"
            : null;
}
