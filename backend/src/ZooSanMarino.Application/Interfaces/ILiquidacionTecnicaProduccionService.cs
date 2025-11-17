// src/ZooSanMarino.Application/Interfaces/ILiquidacionTecnicaProduccionService.cs
using ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para cálculos de liquidación técnica de producción diaria
/// A partir de la semana 26, organizado por etapas
/// </summary>
public interface ILiquidacionTecnicaProduccionService
{
    /// <summary>
    /// Calcula la liquidación técnica de producción para un lote
    /// Organizado por etapas: 1 (25-33), 2 (34-50), 3 (>50)
    /// </summary>
    Task<LiquidacionTecnicaProduccionDto> CalcularLiquidacionProduccionAsync(LiquidacionTecnicaProduccionRequest request);
    
    /// <summary>
    /// Verifica si un lote tiene datos de producción diaria a partir de semana 26
    /// </summary>
    Task<bool> ValidarLoteParaLiquidacionProduccionAsync(int loteId);
    
    /// <summary>
    /// Obtiene el resumen de una etapa específica
    /// </summary>
    Task<EtapaLiquidacionDto?> ObtenerResumenEtapaAsync(int loteId, int etapa);
}




