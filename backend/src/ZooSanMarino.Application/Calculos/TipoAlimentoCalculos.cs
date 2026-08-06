namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Recorte defensivo de <c>tipo_alimento</c> en las tablas de seguimiento diario.
///
/// <para><b>Por qué existe (incidente 2026-08-06, lote A374A de Sanmarino Colombia).</b> El cliente arma
/// <c>tipo_alimento</c> concatenando los nombres de los alimentos del día (<c>"H: … / M: … / G: …"</c>),
/// así que su largo crece con la cantidad de ítems y con el largo de los nombres del catálogo. La columna
/// era <c>varchar(100)</c> y la pantalla no limita cuántos alimentos se agregan: con los nombres de
/// reproductora (30–35 caracteres) el TERCER alimento pasaba de 100 y Postgres abortaba el INSERT con
/// <c>22001</c>. Como el alta de Colombia corre en una transacción atómica, se perdía el guardado
/// completo — el usuario veía «An error occurred while saving the entity changes» y nada quedaba grabado.</para>
///
/// <para><b>Qué es y qué no es.</b> <c>tipo_alimento</c> es una cadena de PRESENTACIÓN (tabla diaria y
/// Excel). El dato real por ítem vive en <c>metadata.itemsHembras/itemsMachos/itemsGenerales</c> y es el
/// que alimenta consumo, inventario y cálculos. Recortar esta cadena no altera ninguna aritmética.</para>
///
/// <para><b>Red de seguridad, no ruta feliz.</b> La columna se amplió a <see cref="MaxLongitud"/>
/// (migración <c>AmpliarTipoAlimentoSeguimientos</c>); con nombres de ≤35 caracteres harían falta unos 14
/// alimentos en un mismo día para llegar al tope. Este recorte existe para que un catálogo con nombres
/// largos no pueda volver a tumbar un guardado entero.</para>
/// </summary>
public static class TipoAlimentoCalculos
{
    /// <summary>
    /// Largo de <c>tipo_alimento</c> en TODAS las tablas de seguimiento diario (levante y engorde).
    /// Debe coincidir con el <c>HasMaxLength</c> de sus configurations y con el DDL de las migraciones
    /// <c>AmpliarTipoAlimentoSeguimientos</c> (levante) y <c>AmpliarTipoAlimentoEngorde</c> (engorde,
    /// que además recrea las 3 vistas de Power BI que cuelgan de la columna).
    /// </summary>
    public const int MaxLongitud = 500;

    /// <summary>
    /// Devuelve <paramref name="valor"/> recortado a <paramref name="max"/> caracteres.
    /// Conserva el PREFIJO (sin puntos suspensivos, igual que el recorte que ya existía en la carga
    /// masiva): no se inventan caracteres que el usuario no eligió. <c>null</c> y <c>""</c> pasan
    /// tal cual, y un valor que ya entra se devuelve intacto (misma instancia).
    /// </summary>
    /// <param name="valor">Cadena a persistir. Puede ser <c>null</c>.</param>
    /// <param name="max">Tope de caracteres. Por defecto <see cref="MaxLongitud"/>. Valores ≤ 0 no recortan.</param>
    public static string? Recortar(string? valor, int max = MaxLongitud)
    {
        if (valor is null || max <= 0 || valor.Length <= max) return valor;
        return valor[..max];
    }

    /// <summary>
    /// <c>true</c> si <see cref="Recortar"/> efectivamente cortaría texto. Sirve para dejar un
    /// <c>LogWarning</c> cuando el recorte actúa, y que un catálogo mal cargado no pase inadvertido.
    /// </summary>
    public static bool Recorta(string? valor, int max = MaxLongitud) =>
        valor is not null && max > 0 && valor.Length > max;
}
