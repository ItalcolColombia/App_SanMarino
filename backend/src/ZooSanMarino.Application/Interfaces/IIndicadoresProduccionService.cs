using ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para calcular indicadores semanales de producción diaria
/// </summary>
public interface IIndicadoresProduccionService
{
    /// <summary>
    /// Obtiene indicadores semanales de producción agrupados por semana
    /// Compara con guía genética cuando está disponible
    /// </summary>
    Task<IndicadoresProduccionResponse> ObtenerIndicadoresSemanalesAsync(IndicadoresProduccionRequest request);
    
    /// <summary>
    /// Obtiene indicadores para una semana específica
    /// </summary>
    Task<IndicadorProduccionSemanalDto?> ObtenerIndicadorSemanaAsync(int loteId, int semana);
}




