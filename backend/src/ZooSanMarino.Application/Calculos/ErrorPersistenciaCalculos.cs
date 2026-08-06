namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Traduce el <c>SqlState</c> de un error de Postgres a un mensaje accionable en español.
///
/// <para><b>Por qué existe (incidente 2026-08-06).</b> El handler global devolvía <c>ex.Message</c> tal
/// cual; para un <c>DbUpdateException</c> eso es el texto genérico de EF
/// («An error occurred while saving the entity changes. See the inner exception for details.»), que llega
/// al toast del usuario sin decir nada. El <c>SqlState</c> real —el único dato útil— nunca salía del
/// servidor, y diagnosticar el 500 exigía reproducirlo con el backend en local.</para>
///
/// <para><b>Estrictamente aditivo.</b> Ante un <c>SqlState</c> no mapeado devuelve <c>null</c> y el
/// handler cae al mensaje de hoy: los errores que ya se muestran bien no cambian. Los mensajes no
/// exponen nombres de tablas ni de columnas — describen QUÉ hacer, no el detalle interno (ese sigue
/// yendo completo al log del servidor).</para>
/// </summary>
public static class ErrorPersistenciaCalculos
{
    /// <summary>
    /// Mensaje para el usuario según el <c>SqlState</c> de Postgres, o <c>null</c> si no está mapeado.
    /// </summary>
    /// <param name="sqlState">Código SQLSTATE de 5 caracteres (p. ej. <c>22001</c>).</param>
    public static string? DescribirErrorSql(string? sqlState) => (sqlState ?? "").Trim() switch
    {
        // 22001 string_data_right_truncation — el caso del incidente: la lista de alimentos concatenada
        // superaba el largo de la columna.
        "22001" => "Uno de los textos del registro supera el largo permitido. Revisá los campos de texto largos "
                 + "(por ejemplo, la lista de alimentos del día) y volvé a guardar.",

        // 23505 unique_violation
        "23505" => "Ya existe un registro con esos mismos datos. Revisá si el registro de esa fecha ya fue creado.",

        // 23503 foreign_key_violation
        "23503" => "El registro hace referencia a un dato que no existe o fue eliminado. Refrescá la pantalla y volvé a intentar.",

        // 23502 not_null_violation
        "23502" => "Falta un dato obligatorio para guardar el registro.",

        // 23514 check_violation
        "23514" => "Alguno de los valores no cumple una regla de validación de la base de datos.",

        // 22003 numeric_value_out_of_range
        "22003" => "Uno de los valores numéricos está fuera del rango permitido.",

        _ => null
    };
}
