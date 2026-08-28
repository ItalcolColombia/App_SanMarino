namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Traduce la violación de unicidad de un seguimiento diario al mensaje que ve el usuario.
///
/// <para>
/// El invariante «un registro por lote y por día» lo sostienen DOS índices que dicen lo mismo con
/// distinto alcance, y el usuario no tiene por qué notar la diferencia:
/// </para>
/// <list type="bullet">
///   <item><c>uq_seg_diario_aves_engorde_lote_fecha</c> — sobre <c>(lote, fecha)</c> con la fecha
///   como <c>timestamptz</c>: sólo caza dos filas guardadas con el MISMO instante.</item>
///   <item><c>ux_seg_diario_aves_engorde_lote_dia_utc</c> — sobre <c>(lote, día UTC)</c>: caza el día
///   calendario, que es el invariante de verdad. Es el que dispara cuando el formulario manual
///   (mediodía UTC) choca con una fila del cruce de reproductora (medianoche UTC), el caso que el
///   índice viejo dejaba pasar en silencio.</item>
/// </list>
///
/// <para>
/// Vive acá y no inline en el controller porque el nombre del índice es un <b>contrato con la BD</b>:
/// si una migración lo renombra y nadie actualiza esta lista, el usuario deja de ver el mensaje claro
/// y recibe el texto crudo de Postgres — una regresión silenciosa que ningún build detecta. El test
/// fija los dos nombres.
/// </para>
/// </summary>
public static class DuplicadoSeguimientoDiarioCalculos
{
    /// <summary>Índices que significan «ya hay un registro de ese lote para ese día».</summary>
    private static readonly string[] IndicesUnDiaPorLote =
    [
        "uq_seg_diario_aves_engorde_lote_fecha",
        "ux_seg_diario_aves_engorde_lote_dia_utc"
    ];

    /// <summary>
    /// True si la violación de unicidad viene de intentar guardar un segundo registro para el mismo
    /// lote y el mismo día. <c>null</c> o un índice desconocido devuelven <c>false</c>: se prefiere
    /// caer al mensaje genérico —que incluye el detalle de Postgres— antes que afirmar una causa que
    /// no se verificó.
    /// </summary>
    public static bool EsUnRegistroPorLotePorDia(string? nombreIndice) =>
        nombreIndice is not null &&
        IndicesUnDiaPorLote.Contains(nombreIndice, StringComparer.OrdinalIgnoreCase);

    /// <summary>Mensaje único para los dos índices: para el usuario es el mismo problema.</summary>
    public const string MensajeUnRegistroPorLotePorDia =
        "Ya existe un registro de seguimiento diario para este lote en la fecha seleccionada. " +
        "Solo puede haber un registro por lote por día.";
}
