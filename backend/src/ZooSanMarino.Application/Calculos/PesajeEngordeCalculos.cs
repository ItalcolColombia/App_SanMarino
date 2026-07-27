// src/ZooSanMarino.Application/Calculos/PesajeEngordeCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo PURO del <b>día de pesaje obligatorio</b> de un lote de pollo engorde.
/// <para>
/// Regla de negocio: durante la primera semana se pesa <b>todos los días</b> y después
/// <b>una vez por semana</b>, al cierre de cada semana (día 14, 21, 28…).
/// </para>
/// <para>
/// El número sobre el que se evalúa la regla depende de la empresa, y por eso entra como parámetro:
/// </para>
/// <list type="bullet">
///   <item>
///     Empresa <b>con</b> la regla de la hora de llegada (<c>primer_registro_segun_hora_llegada</c>):
///     se evalúa sobre el <b>día de negocio</b> (el primer día con registro es el día 1), así el
///     pesaje semanal cae en el ÚLTIMO día de la semana.
///   </item>
///   <item>
///     Empresa <b>sin</b> la regla: se evalúa sobre la <b>edad</b> cruda (0 el día del encasetamiento),
///     que es literalmente el comportamiento histórico — mismo set de días, byte a byte.
///   </item>
/// </list>
/// </summary>
public static class PesajeEngordeCalculos
{
    /// <summary>Días de la primera semana, en los que el pesaje es diario.</summary>
    public const int DiasPesajeDiario = 7;

    /// <summary>
    /// <c>true</c> si el número de día recibido es día de pesaje obligatorio: 1..7 (diario) o, a
    /// partir del 8, cada múltiplo de 7. Días ≤ 0 (anteriores al primer registro) ⇒ <c>false</c>.
    /// </summary>
    public static bool EsDiaDePesajeObligatorio(int dia) =>
        (dia >= 1 && dia <= DiasPesajeDiario) || (dia > DiasPesajeDiario && dia % DiasPesajeDiario == 0);

    /// <summary>
    /// Número de día sobre el que se evalúa la regla de pesaje: el día de negocio si la empresa tiene
    /// activa la regla de la hora de llegada, o la edad cruda si no (comportamiento previo intacto).
    /// </summary>
    public static int DiaParaReglaDePesaje(int edad, int diaDeNegocio, bool reglaHoraActiva) =>
        reglaHoraActiva ? diaDeNegocio : edad;

    /// <summary>
    /// Punto de entrada completo: resuelve el día que corresponde según la empresa y aplica la regla.
    /// </summary>
    public static bool EsDiaDePesajeObligatorio(
        DateTime fecha, DateTime fechaEncasetamiento, TimeOnly? horaEncasetamiento, bool reglaHoraActiva)
    {
        var edad = (int)(fecha.Date - fechaEncasetamiento.Date).TotalDays;
        var horaEfectiva = EncasetamientoCalculos.HoraEfectiva(horaEncasetamiento, reglaHoraActiva);
        var dia = EncasetamientoCalculos.DiaDeNegocio(fecha, fechaEncasetamiento, horaEfectiva);
        return EsDiaDePesajeObligatorio(DiaParaReglaDePesaje(edad, dia, reglaHoraActiva));
    }
}
