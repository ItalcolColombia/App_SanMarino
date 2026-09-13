// src/ZooSanMarino.Application/Calculos/PesajeSemanalLevanteCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// ESPECIFICACIÓN EJECUTABLE del bloque «Pesaje» de <c>fn_indicadores_levante_postura</c> (la fn SQL
/// es la dueña del número; estos tests son su contrato — regla «una sola fórmula por número»).
/// Decide qué peso y qué uniformidad representan a la SEMANA en los indicadores y la gráfica de levante.
///
/// Regla (13-sep-2026, varios registros por día — plan indicadores_semanales_varios_registros_dia):
///  • Día de pesaje = el ÚLTIMO día de la semana con algún registro que pesó (PesoH &gt; 0 o PesoM &gt; 0).
///  • Peso por sexo = promedio de los registros de ESE día que pesaron ese sexo (&gt; 0); 0 si ninguno.
///  • Uniformidad por sexo = la del último registro (mayor Id) de ese día que la trae (&gt; 0); 0 si ninguno.
///  • Semana sin ningún pesaje: los valores del último registro de la semana (por día y luego Id).
/// Con un registro con pesaje por día equivale a la regla anterior («el último registro con peso &gt; 0»).
/// Los ceros significan «no se midió»: la fn lee con COALESCE(..., 0).
/// Sin EF ni estado: función pura.
/// </summary>
public static class PesajeSemanalLevanteCalculos
{
    /// <summary>Registro de levante de la semana, ya con los nulos convertidos a 0 (como la fn).</summary>
    public sealed record RegistroSemana(long Id, DateOnly Dia, double PesoH, double PesoM, double UnifH, double UnifM);

    /// <summary>Peso y uniformidad que representan a la semana.</summary>
    public readonly record struct Pesaje(double PesoH, double PesoM, double UnifH, double UnifM);

    /// <summary>Aplica la regla de la clase a los registros de UNA semana.</summary>
    public static Pesaje PesajeDeLaSemana(IEnumerable<RegistroSemana> registros)
    {
        var lista = registros.ToList();
        if (lista.Count == 0)
            return new Pesaje(0, 0, 0, 0);

        var conPesaje = lista.Where(r => r.PesoH > 0 || r.PesoM > 0).ToList();
        if (conPesaje.Count == 0)
        {
            var ultimo = lista.OrderBy(r => r.Dia).ThenBy(r => r.Id).Last();
            return new Pesaje(ultimo.PesoH, ultimo.PesoM, ultimo.UnifH, ultimo.UnifM);
        }

        var dia = conPesaje.Max(r => r.Dia);
        var delDia = conPesaje.Where(r => r.Dia == dia).OrderBy(r => r.Id).ToList();
        return new Pesaje(
            PesoH: PromedioDeLosQueMidieron(delDia.Select(r => r.PesoH)),
            PesoM: PromedioDeLosQueMidieron(delDia.Select(r => r.PesoM)),
            UnifH: delDia.LastOrDefault(r => r.UnifH > 0)?.UnifH ?? 0,
            UnifM: delDia.LastOrDefault(r => r.UnifM > 0)?.UnifM ?? 0);
    }

    /// <summary>≙ COALESCE(AVG(x) FILTER (WHERE x &gt; 0), 0).</summary>
    private static double PromedioDeLosQueMidieron(IEnumerable<double> valores)
    {
        var positivos = valores.Where(v => v > 0).ToList();
        return positivos.Count == 0 ? 0 : positivos.Average();
    }
}
