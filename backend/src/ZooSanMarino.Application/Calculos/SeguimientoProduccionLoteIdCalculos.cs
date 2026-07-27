// Cálculo PURO de la resolución del LoteId efectivo del seguimiento diario de producción.
// Sin EF, sin estado, sin I/O: solo decide qué lote base usar cuando la fila de
// lote_postura_produccion quedó sin lote_id.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Reglas puras para resolver el <c>lote_id</c> con el que se guarda el seguimiento diario de
/// producción (<c>seguimiento_diario_produccion.lote_id</c> es NOT NULL en BD).
/// <para>
/// Los lotes de producción (LPP) nacen SOLO al cerrar un levante; antes del fix de herencia ese
/// cierre no copiaba <c>LoteId</c>/<c>LotePadreId</c>, dejando filas con <c>lote_id</c> NULL cuyo
/// levante de origen SÍ lo tiene. Por eso, si el LPP no trae un lote válido, se hereda el del
/// levante; si ninguno de los dos lo tiene, no hay lote base y el guardado debe rechazarse.
/// </para>
/// </summary>
public static class SeguimientoProduccionLoteIdCalculos
{
    /// <summary>
    /// Devuelve <paramref name="loteIdProduccion"/> si es &gt; 0 (el propio manda, no se pisa);
    /// si no, <paramref name="loteIdLevante"/> si es &gt; 0 (herencia del levante para filas
    /// creadas antes del fix del cierre); si no, <c>null</c> (irresoluble → error claro arriba).
    /// </summary>
    public static int? ResolverLoteIdEfectivo(int? loteIdProduccion, int? loteIdLevante)
    {
        if (loteIdProduccion is > 0) return loteIdProduccion;
        if (loteIdLevante is > 0) return loteIdLevante;
        return null;
    }
}
