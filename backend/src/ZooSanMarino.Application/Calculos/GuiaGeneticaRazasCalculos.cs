// src/ZooSanMarino.Application/Calculos/GuiaGeneticaRazasCalculos.cs
// Cómo se arma la lista de razas que ve el selector de lotes cuando la empresa tiene DOS fuentes de
// guía genética. Sin EF, sin estado, sin I/O.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Combinación de las razas disponibles cuando una empresa tiene guía propia (tabla reducida) y
/// además filas en la guía compartida.
///
/// <para>
/// 🔴 <b>El defecto que corrige.</b> <c>GuiaGeneticaService.ObtenerRazasCrudoAsync</c> cortaba a
/// nivel <b>EMPRESA</b>, no de raza:
/// </para>
/// <code>
/// var propias = await _ctx.GuiaGeneticaSantaReyes…ToListAsync();
/// if (propias.Count > 0) return propias;   // ← con 615 filas sembradas, SIEMPRE entra acá
/// </code>
/// <para>
/// Consecuencia medida: una raza cargada en la guía compartida se importaba «OK», se veía en el
/// grid y <b>nunca aparecía en el selector de lotes</b>, porque la primera consulta ya había
/// devuelto algo. No fallaba: mentía. Y era el único workaround aparente para una raza sin guía
/// propia (<c>Lohmann Brown</c>, que a propósito no tiene alias — ver <see cref="RazaGuiaAliasCalculos"/>).
/// </para>
///
/// <para>
/// 🔴 <b>Condición innegociable — delta cero.</b> Para una empresa <b>sin</b> guía propia la salida
/// tiene que ser byte a byte la de hoy: la misma lista, en el mismo orden, sin deduplicar ni
/// reordenar nada. Por eso <see cref="CombinarRazas"/> devuelve la lista compartida <b>tal cual la
/// recibió</b> —la misma instancia— cuando no hay propias, y sólo entra a combinar en el caso nuevo.
/// Sanmarino, Demo, Ecuador y Panamá (medido: 0 filas propias las cuatro) no pueden moverse.
/// </para>
/// </summary>
public static class GuiaGeneticaRazasCalculos
{
    /// <summary>
    /// Razas visibles para la empresa: las de su guía propia primero (grafía canónica del cliente) y
    /// a continuación las de la guía compartida que no estén ya representadas.
    ///
    /// <para>
    /// <b>Sin propias ⇒ se devuelve <paramref name="compartidas"/> intacta</b> (misma instancia,
    /// mismo orden): es la rama de siempre y no puede cambiar un byte.
    /// </para>
    ///
    /// <para>
    /// <b>Con propias</b> se concatena sin duplicar. El duplicado se detecta por raza normalizada
    /// (recortada y en minúsculas, el mismo criterio de <see cref="RazaGuiaAliasCalculos.Normalizar"/>),
    /// así que <c>«Babcock Brown»</c> y <c>«BABCOCK BROWN»</c> cuentan como una sola y gana la grafía
    /// de la guía propia — que es la que el resto del sistema usa para cruzar contra la tabla.
    /// </para>
    /// </summary>
    /// <param name="propias">Razas de la guía propia (tabla reducida) de esta empresa.</param>
    /// <param name="compartidas">Razas de la guía compartida de esta empresa.</param>
    public static List<string> CombinarRazas(List<string>? propias, List<string> compartidas)
    {
        // Rama de siempre: sin guía propia no hay nada que combinar y la salida es la de hoy.
        if (propias is null || propias.Count == 0) return compartidas;

        var combinadas = new List<string>(propias.Count + compartidas.Count);
        var vistas = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raza in propias)
        {
            if (raza is null) continue;
            if (vistas.Add(RazaGuiaAliasCalculos.Normalizar(raza))) combinadas.Add(raza);
        }

        foreach (var raza in compartidas)
        {
            if (raza is null) continue;
            if (vistas.Add(RazaGuiaAliasCalculos.Normalizar(raza))) combinadas.Add(raza);
        }

        return combinadas;
    }
}
