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
    /// Largo de <c>seguimiento_diario_levante.tipo_alimento</c>. Debe coincidir con el
    /// <c>HasMaxLength</c> de <c>SeguimientoDiarioConfiguration</c> y con el DDL de la migración
    /// <c>AmpliarTipoAlimentoSeguimientos</c>.
    /// </summary>
    public const int MaxLongitud = 500;

    /// <summary>
    /// Largo de <c>tipo_alimento</c> en las tablas de seguimiento de ENGORDE, que siguen en 100.
    ///
    /// <para><b>Por qué no se ampliaron junto con levante.</b> La vista de Power BI
    /// <c>vw_seguimiento_pollo_engorde</c> depende de <c>seguimiento_diario_aves_engorde.tipo_alimento</c>,
    /// y PostgreSQL rechaza el <c>ALTER COLUMN … TYPE</c> con <c>0A000 cannot alter type of a column used
    /// by a view or rule</c>. Ampliarla exigiría dropear y recrear esa vista dentro de una migración que
    /// se aplica sola en cada deploy, con riesgo de perder sus permisos sin que nadie lo note. Engorde no
    /// es el módulo del incidente, así que acá alcanza con el recorte: el texto se acorta, pero el
    /// guardado nunca vuelve a caerse con 22001.</para>
    /// </summary>
    public const int MaxLongitudEngorde = 100;

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
