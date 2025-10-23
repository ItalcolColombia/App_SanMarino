// src/ZooSanMarino.Application/DTOs/LiquidacionTecnicaDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para los resultados de liquidación técnica de un lote de levante a la semana 25
/// </summary>
public record LiquidacionTecnicaDto(
    string LoteId,
    string LoteNombre,
    DateTime FechaEncaset,
    string? Raza,
    int? AnoTablaGenetica,
    string? CodigoGuiaGenetica,
    
    // Datos iniciales
    int? HembrasEncasetadas,
    int? MachosEncasetados,
    int? TotalAvesEncasetadas,
    
    // Mortalidad Acumulada
    decimal PorcentajeMortalidadHembras,
    decimal PorcentajeMortalidadMachos,
    
    // Selección Acumulada
    decimal PorcentajeSeleccionHembras,
    decimal PorcentajeSeleccionMachos,
    
    // Error de Sexaje Acumulado
    decimal PorcentajeErrorSexajeHembras,
    decimal PorcentajeErrorSexajeMachos,
    
    // Retiro Total Acumulado (Mortalidad + Selección + Error)
    decimal PorcentajeRetiroTotalHembras,
    decimal PorcentajeRetiroTotalMachos,
    decimal PorcentajeRetiroTotalGeneral,
    
    // Retiro Guía
    decimal? PorcentajeRetiroGuia,
    
    // Consumo de Alimentos
    decimal ConsumoAlimentoRealGramos,
    decimal? ConsumoAlimentoGuiaGramos,
    decimal? PorcentajeDiferenciaConsumo,
    
    // Peso a la Semana 25
    decimal? PesoSemana25RealHembras,
    decimal? PesoSemana25RealMachos,
    decimal? PesoSemana25GuiaHembras,
    decimal? PesoSemana25GuiaMachos,
    decimal? PorcentajeDiferenciaPesoHembras,
    decimal? PorcentajeDiferenciaPesoMachos,
    
    // Uniformidad
    decimal? UniformidadRealHembras,
    decimal? UniformidadRealMachos,
    decimal? UniformidadGuiaHembras,
    decimal? UniformidadGuiaMachos,
    decimal? PorcentajeDiferenciaUniformidadHembras,
    decimal? PorcentajeDiferenciaUniformidadMachos,
    
    // Metadatos del cálculo
    DateTime FechaCalculo,
    int TotalRegistrosSeguimiento,
    DateTime? FechaUltimoSeguimiento
);

/// <summary>
/// DTO para solicitar el cálculo de liquidación técnica
/// </summary>
public record LiquidacionTecnicaRequest(
    int LoteId,
    DateTime? FechaHasta = null // Si no se especifica, usa la fecha actual
);

/// <summary>
/// DTO con detalles del seguimiento diario para auditoría
/// </summary>
public record DetalleSeguimientoLiquidacionDto(
    DateTime Fecha,
    int Semana,
    int MortalidadHembras,
    int MortalidadMachos,
    int SeleccionHembras,
    int SeleccionMachos,
    int ErrorSexajeHembras,
    int ErrorSexajeMachos,
    double ConsumoKgHembras,
    double? ConsumoKgMachos,
    double? PesoPromHembras,
    double? PesoPromMachos,
    double? UniformidadHembras,
    double? UniformidadMachos
);

/// <summary>
/// DTO completo con liquidación y detalles
/// </summary>
public record LiquidacionTecnicaCompletaDto(
    LiquidacionTecnicaDto Liquidacion,
    List<DetalleSeguimientoLiquidacionDto> DetalleSeguimiento,
    DatosGuiaGeneticaDto? DatosGuia
);

/// <summary>
/// DTO para datos de la guía genética
/// </summary>
public record DatosGuiaGeneticaDto(
    string? AnioGuia,
    string? Raza,
    string? Edad,
    decimal? PesoHembras,
    decimal? PesoMachos,
    decimal? Uniformidad,
    decimal? ConsumoAcumulado,
    decimal? PorcentajeRetiro
);
