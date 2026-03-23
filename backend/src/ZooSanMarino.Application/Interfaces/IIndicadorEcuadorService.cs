// src/ZooSanMarino.Application/Interfaces/IIndicadorEcuadorService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para cálculo de indicadores técnicos de Ecuador
/// </summary>
public interface IIndicadorEcuadorService
{
    /// <summary>
    /// Calcula indicadores para lotes activos o cerrados según filtros
    /// </summary>
    Task<IEnumerable<IndicadorEcuadorDto>> CalcularIndicadoresAsync(IndicadorEcuadorRequest request);
    
    /// <summary>
    /// Calcula indicadores consolidados de todas las granjas
    /// </summary>
    Task<IndicadorEcuadorConsolidadoDto> CalcularConsolidadoAsync(IndicadorEcuadorRequest request);
    
    /// <summary>
    /// Calcula liquidación por período (semanal o mensual)
    /// </summary>
    Task<LiquidacionPeriodoDto> CalcularLiquidacionPeriodoAsync(
        DateTime fechaInicio,
        DateTime fechaFin,
        string tipoPeriodo, // "Semanal" o "Mensual"
        int? granjaId = null
    );
    
    /// <summary>
    /// Si <paramref name="soloCerrados"/> es true: solo lotes con aves = 0 cuya <b>fecha de cierre</b> cae en [fechaDesde, fechaHasta].
    /// Si es false: lotes (abiertos y cerrados) cuyo <b>encaset</b> cae en el mismo rango.
    /// </summary>
    Task<IEnumerable<IndicadorEcuadorDto>> ObtenerLotesCerradosAsync(
        DateTime fechaDesde,
        DateTime fechaHasta,
        int? granjaId = null,
        bool soloCerrados = true
    );

    /// <summary>
    /// Calcula indicadores de pollo engorde para el lote padre (LoteAveEngorde) y cada lote reproductor asociado.
    /// Usa tablas: lote_ave_engorde, lote_reproductora_ave_engorde, movimiento_pollo_engorde,
    /// seguimiento_diario_aves_engorde, seguimiento_diario_lote_reproductora_aves_engorde.
    /// </summary>
    Task<IndicadorPolloEngordePorLotePadreDto> CalcularIndicadoresPolloEngordePorLotePadreAsync(
        IndicadorPolloEngordePorLotePadreRequest request
    );

    /// <summary>
    /// Liquidación Pollo Engorde: solo lotes padre liquidados (sin reproductoras).
    /// Modo UnLote: <paramref name="request"/>.LoteAveEngordeId obligatorio.
    /// Modo Rango: fechas obligatorias; franja por fecha de cierre; alcance TodasLasGranjas / Granja / Nucleo.
    /// </summary>
    Task<LiquidacionPolloEngordeReporteDto> LiquidacionPolloEngordeReporteAsync(
        LiquidacionPolloEngordeReporteRequest request,
        CancellationToken ct = default);
}
