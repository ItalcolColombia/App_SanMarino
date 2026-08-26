// src/ZooSanMarino.Application/Calculos/RazaGuiaAliasCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Alias de lectura entre la grafía de raza que trae el ERP del cliente y la grafía con la que está
/// cargada su guía genética propia (<c>guia_genetica_santa_reyes</c>).
///
/// <para>
/// <b>Por qué existe.</b> Los lotes se cargan con el nombre de raza tal como viene del ERP
/// (<c>BABCOK BROWN</c> sin la segunda C, <c>HY LINE</c> sin el apellido), mientras que la guía se
/// sembró con el nombre comercial completo (<c>Babcock Brown</c>, <c>Hy Line Brown</c>). Medido en
/// BD el 24-ago-2026: <b>3 de las 4 razas</b> de los lotes reales de Santa Reyes no cruzaban con su
/// guía, así que los reportes técnicos salían sin una sola columna de comparación y la validación
/// «raza/año obligatorios si hay guía» rechazaba razas que sí estaban cargadas.
/// </para>
///
/// <para>
/// <b>Decisión del usuario (24-ago-2026):</b> tolerar la grafía del ERP por alias de lectura en vez
/// de reescribir el dato del cliente — el ERP tiene que seguir conciliando con sus propios nombres.
/// </para>
///
/// <para>
/// 🔴 <b>Se aplica SOLO a la guía propia, nunca a la compartida</b>
/// (<c>guia_genetica_sanmarino_colombia</c> / <c>ProduccionAvicolaRaw</c>). Sanmarino, Panamá y
/// Ecuador leen de la compartida y no deben notar absolutamente nada: el delta cero queda
/// garantizado por construcción, igual que en <c>GuiaGeneticaLookup.ObtenerFilasPropiasAsync</c>.
/// </para>
/// </summary>
public static class RazaGuiaAliasCalculos
{
    /// <summary>
    /// Grafía del ERP (normalizada) ⇒ grafía con la que vive la raza en la guía propia. Solo se
    /// mapea lo que es la MISMA línea comercial escrita distinto. <c>Lohmann Brown</c> a propósito
    /// <b>no</b> está acá: es una línea distinta de <c>Lohmann LSL</c> y todavía no tiene guía
    /// cargada — mapearla a otra mostraría datos de un ave que no es esa.
    /// </summary>
    private static readonly Dictionary<string, string> AliasPorRazaNormalizada = new(StringComparer.Ordinal)
    {
        ["babcok brown"] = "babcock brown",
        ["hy line"]      = "hy line brown"
    };

    /// <summary>Normalización canónica de una raza para comparar: recorta y pasa a minúsculas.</summary>
    public static string Normalizar(string? raza) =>
        (raza ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Raza normalizada con la que hay que consultar la guía <b>propia</b>. Si la raza no tiene
    /// alias conocido se devuelve normalizada y sin cambios — nunca se adivina una equivalencia.
    /// </summary>
    public static string AliasGuiaPropia(string? raza)
    {
        var clave = Normalizar(raza);
        return AliasPorRazaNormalizada.TryGetValue(clave, out var alias) ? alias : clave;
    }
}
