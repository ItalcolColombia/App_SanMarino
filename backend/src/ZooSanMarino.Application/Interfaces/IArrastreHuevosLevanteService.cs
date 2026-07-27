using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Arrastre de los huevos capturados en LEVANTE (semana 14+) al primer registro de PRODUCCIÓN,
/// al liquidar el lote de levante.
/// <para>
/// El arrastre es una <b>proyección recalculable</b>: los huevos originales nunca se borran ni se
/// marcan en levante. La fila de producción lleva la marca
/// <c>metadata.arrastreHuevosLevante</c> con lo ya aplicado, de modo que re-ejecutar el arrastre
/// sume únicamente el delta (idempotente) y que
/// <c>ProduccionService.CrearSeguimientoAsync</c> sepa que sobre esa fila SÍ puede sumar el
/// seguimiento del día en vez de rechazarlo por duplicado.
/// </para>
/// </summary>
public interface IArrastreHuevosLevanteService
{
    /// <summary>
    /// Suma las 11 categorías de huevos de todos los seguimientos de levante del lote indicado.
    /// Devuelve <see cref="HuevosClasificacion.Cero"/> si no hay huevos cargados.
    /// </summary>
    Task<HuevosClasificacion> CalcularAcumuladoLevanteAsync(
        int lotePosturaLevanteId,
        int? loteId,
        CancellationToken ct = default);

    /// <summary>
    /// Total de huevos de levante para mostrar en el modal de cierre (totales e incubables).
    /// </summary>
    Task<(int Totales, int Incubables)> ObtenerTotalesParaCierreAsync(
        int lotePosturaLevanteId,
        int? loteId,
        CancellationToken ct = default);

    /// <summary>
    /// Crea (o acumula sobre) la fila de <c>seguimiento_diario_produccion</c> de
    /// <paramref name="fechaLiquidacion"/> con los huevos de levante y deja la marca de arrastre.
    /// <para>
    /// No hace <c>SaveChanges</c> ni maneja transacción: el llamador (el cierre del lote) es el
    /// dueño de la unidad de trabajo. Sí recalcula el espejo de huevos al final.
    /// </para>
    /// <para>No hace nada si el acumulado de levante es cero o si el delta contra lo ya aplicado es cero.</para>
    /// </summary>
    /// <returns>Los huevos efectivamente arrastrados (el delta aplicado).</returns>
    Task<HuevosClasificacion> ArrastrarAsync(
        LotePosturaLevante lev,
        LotePosturaProduccion lpp,
        DateTime fechaLiquidacion,
        int userId,
        CancellationToken ct = default);
}
