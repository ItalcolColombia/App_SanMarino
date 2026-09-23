using System.Text.RegularExpressions;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo PURO: infiere la unidad de medida de un ítem de inventario a partir del texto de su
/// descripción, para catálogos importados (ERP) que no traen una columna de unidad propia.
///
/// <para>
/// Usado por la migración de backfill de <c>item_inventario</c> (Santa Reyes, ítems no-alimento):
/// el Excel fuente trae "Desc. item" pero ninguna columna de unidad. Se busca el token de unidad
/// MÁS A LA DERECHA del texto (las descripciones del ERP terminan casi siempre en "X &lt;cantidad&gt;
/// &lt;unidad&gt;", ej. "AVIYODOX GALON X 20 LITROS"), permitiendo que el token esté pegado a dígitos
/// ("946ML", "50KG"). Si no hay ningún token reconocible, cae al default de <paramref name="tipoItem"/>.
/// </para>
///
/// <para>
/// Vocabulario de salida = el mismo que ya usa <see cref="UnidadInventarioCalculos"/>
/// (<c>kg, und, l, ml, g, lb, saco, dosis, gal</c>). La unidad es una ETIQUETA (no participa de
/// ninguna aritmética de saldo), así que un acierto parcial es corregible después a mano sin riesgo.
/// </para>
/// </summary>
public static class InferenciaUnidadInventarioCalculos
{
    private static readonly (string Patron, string Canon)[] Tokens =
    {
        ("KILOS?", "kg"),
        ("KGS?", "kg"),
        ("GRAMOS?", "g"),
        ("GRS?", "g"),
        ("MLS?", "ml"),
        ("CC", "ml"),
        ("LITROS?", "l"),
        ("LTS?", "l"),
        ("GALONES?", "gal"),
        ("GALON", "gal"),
        ("GLNS?", "gal"),
        ("GL", "gal"),
        ("GAL", "gal"),
        ("DOSIS?", "dosis"),
        ("DS", "dosis"),
        ("SACOS?", "saco"),
        ("MTS?", "und"),
        ("UND", "und"),
        ("UN", "und"),
        ("LBS?", "lb"),
        ("ONZ", "und"),
    };

    private static readonly Dictionary<string, string> DefaultPorTipoItem = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vacuna"] = "dosis",
        ["medicamento"] = "und",
        ["desinfectante"] = "l",
        ["combustible"] = "gal",
        ["empaque"] = "und",
        ["mantenimiento"] = "und",
        ["materia_prima"] = "kg",
        ["insumo"] = "und",
    };

    /// <summary>Última red cuando ni el texto ni el tipo de ítem dan una pista: la misma de <see cref="UnidadInventarioCalculos.UnidadPorDefecto"/> no aplica acá (esto es alimento); para insumos sin pista, "und" es el más neutro.</summary>
    public const string UnidadPorDefecto = "und";

    public static string Inferir(string? descripcion, string? tipoItem)
    {
        var texto = (descripcion ?? "").ToUpperInvariant();

        string? mejor = null;
        var mejorPos = -1;
        foreach (var (patron, canon) in Tokens)
        {
            // Ni precedido ni seguido por letra: permite matchear pegado a dígitos ("946ML") sin
            // colarse dentro de otra palabra ("GRAVA" no matchea "GR").
            foreach (Match m in Regex.Matches(texto, $"(?<![A-ZÑ])({patron})(?![A-ZÑ])"))
            {
                if (m.Index > mejorPos)
                {
                    mejorPos = m.Index;
                    mejor = canon;
                }
            }
        }

        if (mejor is not null) return mejor;

        return DefaultPorTipoItem.TryGetValue(tipoItem ?? "", out var porTipo)
            ? porTipo
            : UnidadPorDefecto;
    }
}
