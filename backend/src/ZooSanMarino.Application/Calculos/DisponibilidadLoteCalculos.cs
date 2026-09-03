// src/ZooSanMarino.Application/Calculos/DisponibilidadLoteCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Aritmética pura de la disponibilidad de un lote de postura (aves y huevos).
///
/// <para>
/// <b>Por qué existe.</b> <c>DisponibilidadLoteService</c> trataba «aves» y «huevos» como ramas
/// excluyentes elegidas por <c>lote.Fase</c>. Un lote en producción tiene las DOS cosas: gallinas
/// vivas y huevos. Peor: <c>ValidarDisponibilidadAvesAsync</c> devuelve <c>false</c> cuando el DTO
/// trae <c>Aves == null</c>, así que rutear a huevos dejaba los traslados de aves bloqueados —
/// medido, con 35.372 aves en dos lotes que ni siquiera tenían producción registrada.
/// </para>
///
/// <para>
/// La decisión de fase NO vive acá: es <c>FaseLoteCalculos.ResolverFaseVisible</c>, que ya era la
/// regla canónica del repo (levante cerrado <b>y</b> fila viva en <c>lote_postura_produccion</c>).
/// Acá vive solo la aritmética del saldo.
/// </para>
/// </summary>
public static class DisponibilidadLoteCalculos
{
    /// <summary>
    /// Bajas de una etapa (levante o producción) para un sexo: <c>mortalidad + selección + error de
    /// sexaje</c>.
    ///
    /// <para>
    /// Es la composición de <c>SaldoAvesLevanteCalculos.BajasNetas</c> —la regla canónica del
    /// repo— <b>menos</b> sus términos de traslado y venta, que en este service llegan por
    /// <c>movimiento_aves</c>: sumar los dos contaría dos veces la misma salida. Medido el
    /// 2-sep-2026, en la tabla de producción esos términos están en 0 (traslado salida, traslado
    /// ingreso y venta), así que hoy no hay diferencia; si algún día se llenan, esta es la decisión
    /// que hay que revisar.
    /// </para>
    ///
    /// <para>
    /// <b>Lo que arregla.</b> La fórmula anterior restaba <i>solo</i> la mortalidad de levante.
    /// Ignoraba la selección (11.032 aves en levante y 12.055 en producción, medidas) y el error de
    /// sexaje (834). Con un lote ya despoblado eso no es un detalle: el lote 14 informaba
    /// <b>10.748</b> hembras disponibles cuando le quedaban <b>324</b>, y ese número autoriza
    /// traslados.
    /// </para>
    /// </summary>
    public static int BajasEtapa(int mortalidad, int seleccion, int errorSexaje) =>
        mortalidad + seleccion + errorSexaje;

    /// <summary>
    /// Aves vivas de un sexo: encasetadas menos lo que salió, más lo que entró, nunca negativo.
    ///
    /// <para>
    /// En <c>lotes</c> (postura) <c>hembras_l</c>/<c>machos_l</c> es la BASE de encasetamiento, no
    /// un saldo vivo —al revés que en engorde—, así que restarle las bajas es correcto y no cuenta
    /// dos veces. Un lote que nunca llegó a producción pasa 0 en <paramref name="bajasProduccion"/>.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué existe <paramref name="ingresos"/> (3-sep-2026).</b> Un lote que RECIBE un traslado
    /// mueve de verdad <c>lote_postura_levante.aves_h_actual</c>/<c>aves_m_actual</c>, pero
    /// <paramref name="iniciales"/> es estático (se fija al crear el lote) y la fórmula vieja solo
    /// restaba <paramref name="retiros"/> —lo que SALIÓ—, nunca sumaba lo que entró. Medido: un lote
    /// receptor con 50 hembras iniciales que recibió 10 seguía informando 50 disponibles después del
    /// traslado, cuando en realidad tenía 60. El número autoriza traslados/ventas, así que
    /// subestimarlo podía rechazar una operación válida por "stock insuficiente".
    /// </para>
    /// </summary>
    /// <param name="iniciales">Encasetadas del sexo (<c>hembras_l</c> / <c>machos_l</c>).</param>
    /// <param name="bajasLevante">Salida de <see cref="BajasEtapa"/> sobre <c>seguimiento_diario_levante</c>.</param>
    /// <param name="bajasProduccion">Ídem sobre <c>seguimiento_diario_produccion</c>; 0 sin producción.</param>
    /// <param name="retiros">Movimientos de aves Completados que salieron del lote (este lote como origen).</param>
    /// <param name="ingresos">Movimientos de aves Completados que entraron al lote (este lote como destino).</param>
    public static int AvesVivas(int iniciales, int bajasLevante, int bajasProduccion, int retiros, int ingresos) =>
        Math.Max(0, iniciales - bajasLevante - bajasProduccion - retiros + ingresos);

    /// <summary>
    /// ¿Hay que informar el bloque de huevos? Solo si el lote tiene una LPP viva; sin ella no hay
    /// producción de la que hablar y el bloque va en <c>null</c>, no en ceros: cero significa
    /// «produjo y ya se transfirió todo», y son cosas distintas para quien mira la pantalla.
    /// </summary>
    public static bool InformaHuevos(int? lotePosturaProduccionId) =>
        lotePosturaProduccionId.HasValue && lotePosturaProduccionId.Value > 0;
}
