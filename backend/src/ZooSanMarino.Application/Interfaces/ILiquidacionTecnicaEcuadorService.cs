// Liquidación Técnica para Ecuador: lote aves de engorde (LoteAveEngordeId).
// Usa lote_ave_engorde y seguimiento_diario_aves_engorde.
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ILiquidacionTecnicaEcuadorService
{
    /// <summary>Calcula la liquidación técnica de un lote de aves de engorde (pollo engorde).</summary>
    Task<LiquidacionTecnicaDto> CalcularLiquidacionAsync(int loteAveEngordeId, DateTime? fechaHasta = null);

    /// <summary>Obtiene la liquidación técnica completa con detalles del seguimiento diario.</summary>
    Task<LiquidacionTecnicaCompletaDto> ObtenerLiquidacionCompletaAsync(int loteAveEngordeId, DateTime? fechaHasta = null);

    /// <summary>Compara los datos del lote con la guía genética.</summary>
    Task<LiquidacionTecnicaComparacionDto> CompararConGuiaGeneticaAsync(int loteAveEngordeId, DateTime? fechaHasta = null);

    /// <summary>Obtiene la comparación completa con detalles y seguimientos.</summary>
    Task<LiquidacionTecnicaComparacionCompletaDto> ObtenerComparacionCompletaAsync(int loteAveEngordeId, DateTime? fechaHasta = null);

    /// <summary>Valida si el lote de aves de engorde existe y tiene datos para liquidación.</summary>
    Task<bool> ValidarLoteParaLiquidacionAsync(int loteAveEngordeId);
}
