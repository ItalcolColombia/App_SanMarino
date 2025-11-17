namespace ZooSanMarino.Application.DTOs.Produccion;

/// <summary>
/// DTO para indicadores de producción semanal
/// Agrupa datos de seguimiento diario por semana y los compara con guía genética
/// </summary>
public record IndicadorProduccionSemanalDto(
    int Semana,
    DateTime FechaInicioSemana,
    DateTime FechaFinSemana,
    int TotalRegistros,
    
    // Mortalidad
    int MortalidadHembras,
    int MortalidadMachos,
    decimal PorcentajeMortalidadHembras,
    decimal PorcentajeMortalidadMachos,
    int MortalidadGuiaHembras,
    int MortalidadGuiaMachos,
    decimal? DiferenciaMortalidadHembras,
    decimal? DiferenciaMortalidadMachos,
    
    // Selección
    int SeleccionHembras,
    decimal PorcentajeSeleccionHembras,
    
    // Consumo (kg)
    decimal ConsumoKgHembras,
    decimal ConsumoKgMachos,
    decimal ConsumoTotalKg,
    decimal ConsumoPromedioDiarioKg,
    decimal? ConsumoGuiaHembras, // g/ave/día
    decimal? ConsumoGuiaMachos, // g/ave/día
    decimal? DiferenciaConsumoHembras,
    decimal? DiferenciaConsumoMachos,
    
    // Producción de Huevos
    int HuevosTotales,
    int HuevosIncubables,
    decimal PromedioHuevosPorDia,
    decimal EficienciaProduccion,
    decimal? HuevosTotalesGuia,
    decimal? HuevosIncubablesGuia,
    decimal? PorcentajeProduccionGuia,
    decimal? DiferenciaHuevosTotales,
    decimal? DiferenciaHuevosIncubables,
    decimal? DiferenciaPorcentajeProduccion,
    
    // Peso Huevo
    decimal? PesoHuevoPromedio,
    decimal? PesoHuevoGuia,
    decimal? DiferenciaPesoHuevo,
    
    // Peso Aves (de pesaje semanal)
    decimal? PesoPromedioHembras,
    decimal? PesoPromedioMachos,
    decimal? PesoGuiaHembras,
    decimal? PesoGuiaMachos,
    decimal? DiferenciaPesoHembras,
    decimal? DiferenciaPesoMachos,
    
    // Uniformidad
    decimal? UniformidadPromedio,
    decimal? UniformidadGuia,
    decimal? DiferenciaUniformidad,
    
    // Coeficiente de Variación
    decimal? CoeficienteVariacionPromedio,
    
    // Clasificadora de Huevos (totales de la semana)
    int HuevosLimpios,
    int HuevosTratados,
    int HuevosSucios,
    int HuevosDeformes,
    int HuevosBlancos,
    int HuevosDobleYema,
    int HuevosPiso,
    int HuevosPequenos,
    int HuevosRotos,
    int HuevosDesecho,
    int HuevosOtro,
    
    // Aves al inicio de la semana
    int AvesHembrasInicioSemana,
    int AvesMachosInicioSemana,
    
    // Aves al final de la semana
    int AvesHembrasFinSemana,
    int AvesMachosFinSemana
);

/// <summary>
/// Request para obtener indicadores semanales
/// </summary>
public record IndicadoresProduccionRequest(
    int LoteId,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    int? SemanaDesde = null,
    int? SemanaHasta = null
);

/// <summary>
/// Response con indicadores semanales
/// </summary>
public record IndicadoresProduccionResponse(
    List<IndicadorProduccionSemanalDto> Indicadores,
    int TotalSemanas,
    int SemanaInicial,
    int SemanaFinal,
    bool TieneDatosGuiaGenetica
);




