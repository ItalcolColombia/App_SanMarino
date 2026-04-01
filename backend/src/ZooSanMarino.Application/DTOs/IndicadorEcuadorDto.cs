// src/ZooSanMarino.Application/DTOs/IndicadorEcuadorDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para solicitar cálculo de indicadores de Ecuador
/// </summary>
public record IndicadorEcuadorRequest(
    int? GranjaId = null,
    string? NucleoId = null,
    string? GalponId = null,
    int? LoteId = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    bool SoloLotesCerrados = false, // Solo lotes con aves = 0
    string TipoLote = "Todos", // "Produccion", "Levante", "Reproductora", "Todos"
    decimal? PesoAjusteVariable = null, // Por defecto 2.7 (Conv. Ajustada)
    decimal? DivisorAjusteVariable = null // Por defecto 4.5 (Conv. Ajustada)
);

/// <summary>
/// DTO con los indicadores calculados para una granja/lote
/// </summary>
public record IndicadorEcuadorDto(
    // Identificación
    int GranjaId,
    string GranjaNombre,
    int? LoteId,
    string? LoteNombre,
    string? GalponId,
    string? GalponNombre,
    
    // Datos básicos (para Pollo Engorde: AvesSacrificadas = aves vendidas/despacho desde movimiento_pollo_engorde; Mortalidad = sacrificio en granja desde seguimiento diario)
    int AvesEncasetadas,
    int AvesSacrificadas, // En Pollo Engorde: aves vendidas/despacho (Venta/Despacho/Retiro). No confundir con sacrificio = Mortalidad (seguimiento diario).
    int Mortalidad,       // Mortalidad + selección del seguimiento diario = sacrificio en granja
    decimal MortalidadPorcentaje,
    decimal SupervivenciaPorcentaje,
    
    // Consumo
    decimal ConsumoTotalAlimentoKg,
    decimal ConsumoAveGramos,
    
    // Producción
    decimal KgCarnePollos,
    decimal PesoPromedioKilos,
    decimal Conversion,
    decimal ConversionAjustada2700,
    
    // Parámetros de ajuste (variables)
    decimal PesoAjusteVariable, // Por defecto 2.7
    decimal DivisorAjusteVariable, // Por defecto 4.5
    
    // Edad y área
    decimal EdadPromedio,
    decimal MetrosCuadrados,
    decimal AvesPorMetroCuadrado,
    decimal KgPorMetroCuadrado,
    
    // Eficiencias
    decimal EficienciaAmericana,
    decimal EficienciaEuropea,
    decimal IndiceProductividad,
    decimal GananciaDia,
    
    // Fechas
    DateTime? FechaInicioLote,
    DateTime? FechaCierreLote,
    bool LoteCerrado // Indica si el lote tiene aves = 0
);

/// <summary>
/// DTO para liquidación por período (semanal/mensual)
/// </summary>
public record LiquidacionPeriodoDto(
    DateTime FechaInicio,
    DateTime FechaFin,
    string TipoPeriodo, // "Semanal", "Mensual"
    int TotalGranjas,
    int TotalLotesCerrados,
    IEnumerable<IndicadorEcuadorDto> Indicadores
);

/// <summary>
/// Request para liquidación por período
/// </summary>
public record LiquidacionPeriodoRequest(
    DateTime FechaInicio,
    DateTime FechaFin,
    string TipoPeriodo, // "Semanal" o "Mensual"
    int? GranjaId = null
);

/// <summary>
/// Request para indicadores de pollo engorde por lote padre (LoteAveEngorde) y sus lotes reproductores
/// </summary>
public record IndicadorPolloEngordePorLotePadreRequest(
    int LoteAveEngordeId,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    bool SoloLotesCerrados = false,
    decimal? PesoAjusteVariable = null,
    decimal? DivisorAjusteVariable = null
);

/// <summary>
/// Indicador de un lote reproductor (nombre + mismo DTO de indicador)
/// </summary>
public record IndicadorReproductorDto(int Id, string NombreLote, IndicadorEcuadorDto Indicador);

/// <summary>
/// Respuesta: indicador del lote padre (null si no está cerrado y se filtró por solo cerrados) + lista de indicadores por lote reproductor.
/// Cuando SoloLotesCerrados está activo, se incluyen todos los reproductores asociados con 0 aves, aunque el lote padre aún tenga aves.
/// </summary>
public record IndicadorPolloEngordePorLotePadreDto(
    IndicadorEcuadorDto? IndicadorLotePadre,
    IReadOnlyList<IndicadorReproductorDto> LotesReproductores
);

/// <summary>
/// Reporte de liquidación Pollo Engorde (Ecuador): solo lote padre liquidado (aves = 0), sin reproductoras.
/// </summary>
public record LiquidacionPolloEngordeReporteRequest(
    string Modo,
    int? LoteAveEngordeId,
    DateTime? FechaDesde,
    DateTime? FechaHasta,
    string Alcance,
    int? GranjaId,
    string? NucleoId,
    /// <summary>Modo UnLote sin lote: filtra lotes liquidados por galpón (opcional).</summary>
    string? GalponId = null
);

public record LiquidacionPolloEngordeItemDto(
    int LoteAveEngordeId,
    string LoteNombre,
    IndicadorEcuadorDto Indicador
);

public record LiquidacionPolloEngordeReporteDto(
    string Modo,
    IReadOnlyList<LiquidacionPolloEngordeItemDto> Items
);

/// <summary>
/// DTO para resumen consolidado de todas las granjas
/// </summary>
public record IndicadorEcuadorConsolidadoDto(
    DateTime FechaCalculo,
    int TotalGranjas,
    int TotalLotes,
    int TotalLotesCerrados,
    
    // Totales consolidados
    int TotalAvesEncasetadas,
    int TotalAvesSacrificadas,
    int TotalMortalidad,
    decimal PromedioMortalidadPorcentaje,
    decimal PromedioSupervivenciaPorcentaje,
    
    // Consumo consolidado
    decimal TotalConsumoAlimentoKg,
    decimal PromedioConsumoAveGramos,
    
    // Producción consolidada
    decimal TotalKgCarnePollos,
    decimal PromedioPesoKilos,
    decimal PromedioConversion,
    decimal PromedioConversionAjustada,
    
    // Promedios
    decimal PromedioEdad,
    decimal TotalMetrosCuadrados,
    decimal PromedioAvesPorMetroCuadrado,
    decimal PromedioKgPorMetroCuadrado,
    
    // Eficiencias promedio
    decimal PromedioEficienciaAmericana,
    decimal PromedioEficienciaEuropea,
    decimal PromedioIndiceProductividad,
    decimal PromedioGananciaDia,
    
    // Detalle por granja
    IEnumerable<IndicadorEcuadorDto> IndicadoresPorGranja
);
