// src/ZooSanMarino.Application/Interfaces/ILiquidacionTecnicaService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para cálculos de liquidación técnica de lotes de levante
/// </summary>
public interface ILiquidacionTecnicaService
{
    /// <summary>
    /// Calcula la liquidación técnica de un lote hasta la semana 25
    /// </summary>
    /// <param name="request">Datos de la solicitud de liquidación</param>
    /// <returns>Resultados de la liquidación técnica</returns>
    Task<LiquidacionTecnicaDto> CalcularLiquidacionAsync(LiquidacionTecnicaRequest request);
    
    /// <summary>
    /// Obtiene la liquidación técnica completa con detalles del seguimiento
    /// </summary>
    /// <param name="request">Datos de la solicitud de liquidación</param>
    /// <returns>Liquidación completa con detalles</returns>
    Task<LiquidacionTecnicaCompletaDto> ObtenerLiquidacionCompletaAsync(LiquidacionTecnicaRequest request);
    
    /// <summary>
    /// Verifica si un lote existe y tiene datos de seguimiento
    /// </summary>
    /// <param name="loteId">ID del lote</param>
    /// <returns>True si el lote existe y tiene seguimiento</returns>
    Task<bool> ValidarLoteParaLiquidacionAsync(int loteId);
}
