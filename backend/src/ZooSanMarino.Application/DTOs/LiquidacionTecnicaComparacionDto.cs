// backend/src/ZooSanMarino.Application/DTOs/LiquidacionTecnicaComparacionDto.cs
using System.ComponentModel.DataAnnotations;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para la comparación de liquidación técnica con guía genética
/// </summary>
public record LiquidacionTecnicaComparacionDto(
    // Datos del lote
    int LoteId,
    string LoteNombre,
    string? Raza,
    int? AnoTablaGenetica,
    int? LineaGeneticaId,
    string? LineaGeneticaNombre,
    
    // Métricas reales del lote
    decimal PorcentajeMortalidadHembras,
    decimal PorcentajeMortalidadMachos,
    decimal ConsumoAcumuladoHembras,
    decimal ConsumoAcumuladoMachos,
    decimal? PesoFinalHembras,
    decimal? PesoFinalMachos,
    decimal? UniformidadFinalHembras,
    decimal? UniformidadFinalMachos,
    
    // Valores esperados de la guía genética
    decimal? MortalidadEsperadaHembras,
    decimal? MortalidadEsperadaMachos,
    decimal? ConsumoEsperadoHembras,
    decimal? ConsumoEsperadoMachos,
    decimal? PesoEsperadoHembras,
    decimal? PesoEsperadoMachos,
    decimal? UniformidadEsperadaHembras,
    decimal? UniformidadEsperadaMachos,
    
    // Diferencias calculadas
    decimal? DiferenciaMortalidadHembras,
    decimal? DiferenciaMortalidadMachos,
    decimal? DiferenciaConsumoHembras,
    decimal? DiferenciaConsumoMachos,
    decimal? DiferenciaPesoHembras,
    decimal? DiferenciaPesoMachos,
    decimal? DiferenciaUniformidadHembras,
    decimal? DiferenciaUniformidadMachos,
    
    // Evaluación de cumplimiento
    bool CumpleMortalidadHembras,
    bool CumpleMortalidadMachos,
    bool CumpleConsumoHembras,
    bool CumpleConsumoMachos,
    bool CumplePesoHembras,
    bool CumplePesoMachos,
    bool CumpleUniformidadHembras,
    bool CumpleUniformidadMachos,
    
    // Resumen general
    int TotalParametrosEvaluados,
    int ParametrosCumplidos,
    decimal PorcentajeCumplimiento,
    string EstadoGeneral,
    
    // Metadatos
    DateTime FechaCalculo,
    int TotalSeguimientos,
    DateTime? UltimaFechaSeguimiento
);

/// <summary>
/// DTO para comparaciones detalladas por parámetro
/// </summary>
public record ComparacionDetalladaDto(
    string Parametro,
    decimal ValorReal,
    decimal? ValorEsperado,
    decimal? Diferencia,
    decimal? Tolerancia,
    bool Cumple,
    string Estado
);

/// <summary>
/// DTO para la comparación completa con detalles y seguimientos
/// </summary>
public record LiquidacionTecnicaComparacionCompletaDto(
    LiquidacionTecnicaComparacionDto Resumen,
    IEnumerable<ComparacionDetalladaDto> ComparacionesDetalladas,
    IEnumerable<DetalleSeguimientoLiquidacionDto> Seguimientos,
    string? Observaciones
);

/// <summary>
/// DTO para líneas genéticas disponibles
/// </summary>
public record LineaGeneticaDisponibleDto(
    string Raza,
    string AnioGuia,
    string CodigoGuia,
    string? Descripcion,
    bool Activo
);