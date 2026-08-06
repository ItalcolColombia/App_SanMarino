// src/ZooSanMarino.Application/Calculos/FaseLoteCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Fase de un lote de postura: <c>Levante</c> o <c>Produccion</c>.
///
/// <para>
/// Al crear un lote la fase se DERIVA de las semanas transcurridas desde el encasetamiento
/// (≥ 26 ⇒ Producción). Eso es correcto para un lote que se da de alta al día, pero rompe la
/// carga de histórico: un lote encasetado hace un año nace en «Producción» y los dos reportes de
/// levante lo filtran (<c>lpl.Etapa == "Levante"</c> en el reporte técnico y
/// <c>l.Fase != "Produccion"</c> en el semanal), así que el dato entra por carga masiva y el
/// reporte no lo ve nunca.
/// </para>
///
/// <para>
/// Por eso la fase pasa a ser un dato OPCIONAL de entrada: si quien crea el lote la indica, manda
/// la indicada; si no, se conserva exactamente la derivación anterior. Es aditivo — un cliente que
/// no envía nada obtiene el mismo resultado de siempre.
/// </para>
/// </summary>
public static class FaseLoteCalculos
{
    public const string Levante = "Levante";
    public const string Produccion = "Produccion";

    /// <summary>Semanas a partir de las cuales un lote se considera en producción.</summary>
    public const int SemanasParaProduccion = 26;

    /// <summary>Las dos fases válidas, para validar la entrada.</summary>
    public static IReadOnlyList<string> Validas { get; } = new[] { Levante, Produccion };

    /// <summary>
    /// Normaliza la fase indicada por el usuario. Devuelve <c>null</c> si viene vacía (⇒ derivar)
    /// y lanza si trae un valor que no es ninguna de las dos fases.
    /// </summary>
    /// <exception cref="ArgumentException">La fase no es <c>Levante</c> ni <c>Produccion</c>.</exception>
    public static string? NormalizarFaseIndicada(string? fase)
    {
        if (string.IsNullOrWhiteSpace(fase)) return null;
        var limpia = fase.Trim();
        foreach (var v in Validas)
            if (string.Equals(limpia, v, StringComparison.OrdinalIgnoreCase))
                return v;
        throw new ArgumentException(
            $"Fase '{fase}' inválida. Usá '{Levante}' o '{Produccion}' (o dejala vacía para que se derive del encasetamiento).");
    }

    /// <summary>Derivación histórica: ≥ 26 semanas desde el encaset ⇒ Producción.</summary>
    public static string DerivarPorEdad(int semanasDesdeEncaset) =>
        semanasDesdeEncaset >= SemanasParaProduccion ? Produccion : Levante;

    /// <summary>
    /// Fase con la que nace el lote: la indicada si vino, y si no la derivada por edad.
    /// Comportamiento previo byte a byte cuando <paramref name="faseIndicada"/> es null/vacía.
    /// </summary>
    public static string Resolver(string? faseIndicada, int semanasDesdeEncaset) =>
        NormalizarFaseIndicada(faseIndicada) ?? DerivarPorEdad(semanasDesdeEncaset);
}
