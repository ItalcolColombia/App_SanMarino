// src/ZooSanMarino.Application/Calculos/EncasetamientoRetroactivoCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo PURO del diagnóstico de informar la <b>hora de encasetamiento</b> en un lote que YA tiene
/// seguimientos cargados (el caso de producción: los lotes existentes se crearon sin hora).
/// <para>
/// El problema: mover el primer día con registro deja los registros ya capturados fuera de la ventana
/// válida. Antes de escribir la hora hay que decir CUÁNTOS registros quedan fuera y en qué fechas, en
/// vez de guardar un 200 que deja el lote en un estado que la propia regla considera inválido.
/// </para>
/// </summary>
public static class EncasetamientoRetroactivoCalculos
{
    /// <summary>
    /// Resultado del diagnóstico. <see cref="Compatible"/> es la única señal que decide si la hora se
    /// puede guardar sin tocar nada.
    /// </summary>
    /// <param name="Compatible">No hay registros anteriores al primer día que impondría la hora.</param>
    /// <param name="RegistrosFuera">Cuántos registros quedarían antes del primer día válido.</param>
    /// <param name="PrimerDia">Primer día con registro que impondría la hora.</param>
    /// <param name="PrimeraFechaFuera">La fecha más temprana que quedaría fuera (para el mensaje).</param>
    public readonly record struct Diagnostico(
        bool Compatible, int RegistrosFuera, DateTime PrimerDia, DateTime? PrimeraFechaFuera);

    /// <summary>
    /// Diagnostica una hora contra las fechas de seguimiento ya cargadas del lote.
    /// <para>
    /// Sin fecha de encaset, sin hora o con hora temprana el resultado es SIEMPRE compatible: la hora
    /// temprana no mueve nada y el lote queda como está. Solo una hora tardía (≥ 13:00) puede dejar
    /// registros fuera, y únicamente los que estén en el día del encasetamiento.
    /// </para>
    /// </summary>
    public static Diagnostico Diagnosticar(
        DateTime? fechaEncasetamiento, TimeOnly? horaEncasetamiento, IEnumerable<DateTime> fechasRegistros)
    {
        if (!fechaEncasetamiento.HasValue)
            return new Diagnostico(true, 0, default, null);

        var primerDia = EncasetamientoCalculos.PrimerDiaConRegistro(fechaEncasetamiento.Value, horaEncasetamiento);
        var fuera = fechasRegistros.Where(f => f.Date < primerDia.Date).ToList();

        return new Diagnostico(
            Compatible: fuera.Count == 0,
            RegistrosFuera: fuera.Count,
            PrimerDia: primerDia,
            PrimeraFechaFuera: fuera.Count == 0 ? null : fuera.Min());
    }

    /// <summary>
    /// Mensaje de rechazo para el usuario: dice qué pasa, con cuántos registros y qué tiene que hacer.
    /// Un 400 explicativo es preferible a un 200 que deja el lote inconsistente en silencio.
    /// </summary>
    public static string MensajeIncompatible(Diagnostico diagnostico, TimeOnly? horaEncasetamiento)
    {
        var motivo = EncasetamientoCalculos.MotivoDesplazamiento(horaEncasetamiento)
                     ?? "la hora informada mueve el primer día con registro";
        return $"No se puede informar esa hora: {motivo}, así que el primer registro pasaría a ser el "
             + $"{diagnostico.PrimerDia:yyyy-MM-dd}, pero el lote ya tiene {diagnostico.RegistrosFuera} "
             + $"registro(s) anteriores (el más antiguo, del {diagnostico.PrimeraFechaFuera:yyyy-MM-dd}). "
             + "Corregí o eliminá esos registros antes de informar la hora.";
    }
}
