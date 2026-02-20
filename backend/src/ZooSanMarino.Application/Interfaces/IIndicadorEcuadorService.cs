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
    /// Obtiene lotes cerrados (aves = 0) en un rango de fechas
    /// </summary>
    Task<IEnumerable<IndicadorEcuadorDto>> ObtenerLotesCerradosAsync(
        DateTime fechaDesde,
        DateTime fechaHasta,
        int? granjaId = null
    );

    /// <summary>
    /// Calcula indicadores de pollo engorde para el lote padre (LoteAveEngorde) y cada lote reproductor asociado.
    /// Usa tablas: lote_ave_engorde, lote_reproductora_ave_engorde, movimiento_pollo_engorde,
    /// seguimiento_diario_aves_engorde, seguimiento_diario_lote_reproductora_aves_engorde.
    /// </summary>
    Task<IndicadorPolloEngordePorLotePadreDto> CalcularIndicadoresPolloEngordePorLotePadreAsync(
        IndicadorPolloEngordePorLotePadreRequest request
    );
}
