// src/ZooSanMarino.Application/Calculos/EtiquetasFiltroInventarioCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Regla PURA que arma las OPCIONES de los desplegables del histórico de inventario (concepto,
/// tipo de ítem, unidad) a partir de los valores que hay en BD.
///
/// <para>
/// Existe porque las listas se armaban con <c>Distinct()</c> sobre el texto crudo mientras TODOS los
/// filtros del API comparan normalizado (<c>Trim().ToLower()</c>): el catálogo tiene el mismo valor
/// escrito con distinta capitalización (<c>Otros insumos</c> / <c>Otros Insumos</c>, <c>und</c> /
/// <c>UND</c>) y el usuario veía dos opciones que devuelven exactamente las mismas filas.
/// </para>
///
/// <para>
/// La migración <c>NormalizarConceptoCatalogoInventario</c> limpia los datos de hoy; esto es la red
/// de seguridad para lo que entre después (un Excel de catálogo mal capitalizado) y para las
/// empresas cuyo catálogo no conocemos. Usa la MISMA regla canónica que la migración —gana la
/// variante más usada— para que la etiqueta de la pantalla y la que queda en BD coincidan.
/// </para>
/// </summary>
public static class EtiquetasFiltroInventarioCalculos
{
    /// <summary>
    /// Clave de agrupación de un valor de filtro: la misma que usan los <c>WHERE</c> del API
    /// (<c>Trim().ToLower()</c>). Dos valores con esta clave igual devuelven las mismas filas.
    /// </summary>
    public static string Normalizar(string? valor) =>
        (valor ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Una sola etiqueta por grupo normalizado, a partir de los valores distintos con su frecuencia.
    ///
    /// <para>
    /// Canónica = la variante <b>más usada</b>; empate ⇒ la que ordena primero con <see cref="StringComparer.Ordinal"/>
    /// (determinismo: la lista no puede depender del orden en que llegaron las filas). Los valores
    /// vacíos o solo espacios se descartan. El resultado sale ordenado con
    /// <see cref="StringComparer.OrdinalIgnoreCase"/>, igual que antes de este cambio.
    /// </para>
    /// </summary>
    public static List<string> EtiquetasUnicas(IEnumerable<(string? Valor, int Usos)> valores)
    {
        var canonicas = new Dictionary<string, (string Etiqueta, int Usos)>(StringComparer.Ordinal);

        foreach (var (valor, usos) in valores)
        {
            var etiqueta = (valor ?? string.Empty).Trim();
            if (etiqueta.Length == 0) continue;

            var clave = Normalizar(etiqueta);
            if (!canonicas.TryGetValue(clave, out var actual))
            {
                canonicas[clave] = (etiqueta, usos);
                continue;
            }

            // Se compara variante contra variante (no el acumulado del grupo): la canónica es la
            // que más ítems/movimientos usan, que es también la que la migración deja en BD.
            var gana = usos > actual.Usos
                || (usos == actual.Usos && string.CompareOrdinal(etiqueta, actual.Etiqueta) < 0);
            if (gana) canonicas[clave] = (etiqueta, usos);
        }

        var lista = canonicas.Values.Select(x => x.Etiqueta).ToList();
        lista.Sort(StringComparer.OrdinalIgnoreCase);
        return lista;
    }
}
