// backend/src/ZooSanMarino.Application/Interfaces/ILiquidacionTecnicaComparacionService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para comparar datos de liquidación técnica con guías genéticas
/// </summary>
public interface ILiquidacionTecnicaComparacionService
{
    /// <summary>
    /// Compara los datos del lote con la guía genética correspondiente
    /// </summary>
    /// <param name="loteId">ID del lote a comparar</param>
    /// <param name="fechaHasta">Fecha límite para el cálculo (opcional)</param>
    /// <returns>Comparación con la guía genética</returns>
    Task<LiquidacionTecnicaComparacionDto> CompararConGuiaGeneticaAsync(int loteId, DateTime? fechaHasta = null);

    /// <summary>
    /// Obtiene la comparación completa con detalles y seguimientos
    /// </summary>
    /// <param name="loteId">ID del lote a comparar</param>
    /// <param name="fechaHasta">Fecha límite para el cálculo (opcional)</param>
    /// <returns>Comparación completa con detalles</returns>
    Task<LiquidacionTecnicaComparacionCompletaDto> ObtenerComparacionCompletaAsync(int loteId, DateTime? fechaHasta = null);

    /// <summary>
    /// Valida si un lote tiene guía genética configurada
    /// </summary>
    /// <param name="loteId">ID del lote a validar</param>
    /// <returns>True si tiene guía genética configurada</returns>
    Task<bool> ValidarGuiaGeneticaConfiguradaAsync(int loteId);

    /// <summary>
    /// Obtiene las líneas genéticas disponibles desde ProduccionAvicolaRaw
    /// </summary>
    /// <param name="raza">Filtrar por raza específica (opcional)</param>
    /// <param name="ano">Filtrar por año específico (opcional)</param>
    /// <returns>Lista de líneas genéticas disponibles</returns>
    Task<IEnumerable<LineaGeneticaDisponibleDto>> ObtenerLineasGeneticasDisponiblesAsync(string? raza = null, int? ano = null);
}