// src/ZooSanMarino.Application/Calculos/AnioGuiaGeneticaCalculos.cs
// Regla PURA de la ESCRITURA de la guía genética: qué `anio_guia` puede llegar a la tabla.
// Sin EF, sin _ctx, sin estado. Plan: fase_de_desarrollo/validacion_anio_guia_genetica_plan.md
using System.Globalization;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Decide si un <c>anio_guia</c> es <b>usable</b> por el sistema antes de dejarlo entrar a cualquiera
/// de las dos tablas de guía genética (<c>guia_genetica_sanmarino_colombia</c> —ancha— y
/// <c>guia_genetica_santa_reyes</c> —reducida—).
///
/// <para>
/// 🔴 <b>El defecto que corrige.</b> Las dos columnas guardan el año como <b>texto libre</b>
/// (<c>text</c> / <c>varchar</c>), pero todo consumidor lo necesita como <b>entero</b>: el destino
/// real es <c>lotes.ano_tabla_genetica</c>, que es <c>integer</c>. Del lado de LECTURA,
/// <c>GuiaGeneticaService.ObtenerAnosDisponiblesAsync</c> ya filtra con
/// <c>int.TryParse</c> —correctamente, porque protege esa columna <c>integer</c>—. El que estaba mal
/// era el lado de la ESCRITURA, que aceptaba en silencio un valor (medido: el literal
/// <c>«G21»</c>, 143 filas) que ese filtro después descartaba sin decir nada, y el error recién se
/// manifestaba mucho más tarde como «No se encontraron años disponibles para la raza X» en el alta
/// de lote.
/// </para>
///
/// <para>
/// <b>Esta regla HONRA ese <c>int.TryParse</c>:</b> si <see cref="EsAnioUsable"/> devuelve
/// <c>true</c>, el año sobrevive al filtro de lectura. Es más estricta en un solo punto —le agrega
/// el rango <see cref="AnioMinimo"/>–<see cref="AnioMaximo"/>—, y ese rango <b>no se inventa</b>: es
/// exactamente el <c>&lt;input type="number" min="1900" max="2100"&gt;</c> que el formulario de lote
/// (<c>lote-list.component.html</c>) ya impone cuando la empresa todavía no tiene guía cargada. Se
/// adopta ese, no otro, para que la validación del año sea <b>una sola</b> en el repo.
/// </para>
///
/// <para>
/// <b>Delta cero.</b> Los 4 valores que hoy viven en la BD (<c>2021</c>, <c>2022</c>, <c>2023</c>,
/// <c>2026</c>, en las dos tablas y las 5 empresas) caen todos dentro del rango ⇒ ninguna carga
/// vigente queda del lado inválido. El único valor que esta regla rechaza y hoy existe es
/// <c>«G21»</c>, que la aplicación nunca pudo usar. Esas 143 filas <b>no se tocan</b> en este
/// trabajo (son dato real del cliente): la validación es sólo para escrituras nuevas.
/// </para>
/// </summary>
public static class AnioGuiaGeneticaCalculos
{
    /// <summary>Primer año aceptado. Igual que el <c>min</c> del input de año del formulario de lote.</summary>
    public const int AnioMinimo = 1900;

    /// <summary>Último año aceptado. Igual que el <c>max</c> del input de año del formulario de lote.</summary>
    public const int AnioMaximo = 2100;

    /// <summary>
    /// <c>true</c> sólo si <paramref name="anio"/>, tras <c>Trim()</c>, parsea a un entero <b>y</b> ese
    /// entero cae en <see cref="AnioMinimo"/>–<see cref="AnioMaximo"/> inclusive.
    ///
    /// <para>
    /// Se usa <see cref="NumberStyles.None"/> con <see cref="CultureInfo.InvariantCulture"/>: sólo
    /// dígitos, sin signo, sin separador de miles, sin punto decimal, sin notación científica. La
    /// forma canónica de un año es un entero de cuatro dígitos y nada más; cualquier otra grafía
    /// (<c>«2.026»</c>, <c>«2026.0»</c>, <c>«20,26»</c>, <c>«2026 AP»</c>, <c>«-2021»</c>,
    /// <c>«G21»</c>) no es un año y se rechaza. <c>null</c>, <c>""</c> y sólo-espacios también son
    /// <c>false</c>: sin año no hay clave natural (<c>codigo = Raza + AnioGuia + Edad</c>) que valga.
    /// </para>
    /// </summary>
    public static bool EsAnioUsable(string? anio)
    {
        if (string.IsNullOrWhiteSpace(anio))
            return false;

        if (!int.TryParse(anio.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var valor))
            return false;

        return valor >= AnioMinimo && valor <= AnioMaximo;
    }

    /// <summary>
    /// Texto de rechazo, único para los tres escritores de guía genética
    /// (<c>ProduccionAvicolaRawService</c>, <c>ExcelImportService</c> y
    /// <c>GuiaGeneticaSantaReyesService</c>) — así los tres dicen lo mismo palabra por palabra. En el
    /// import se antepone <c>«Fila {n}: »</c>; en las altas/ediciones va tal cual dentro de una
    /// <see cref="ArgumentException"/> que el controller traduce a <c>400</c>.
    /// </summary>
    public static string MensajeAnioInvalido(string? anio) =>
        string.IsNullOrWhiteSpace(anio)
            ? $"El año de la guía genética es obligatorio y debe ser un número entero entre {AnioMinimo} y {AnioMaximo}."
            : $"El año de la guía genética «{anio.Trim()}» no es válido: debe ser un número entero entre {AnioMinimo} y {AnioMaximo}.";
}
