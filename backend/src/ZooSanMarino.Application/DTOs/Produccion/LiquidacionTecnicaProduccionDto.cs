// src/ZooSanMarino.Application/DTOs/Produccion/LiquidacionTecnicaProduccionDto.cs
namespace ZooSanMarino.Application.DTOs.Produccion;

/// <summary>
/// DTO para los resultados de liquidación técnica de producción diaria
/// A partir de la semana 26, organizado por etapas
/// </summary>
public record LiquidacionTecnicaProduccionDto(
    string LoteId,
    string LoteNombre,
    DateTime FechaEncaset,
    string? Raza,
    int? AnoTablaGenetica,
    
    // Datos iniciales de producción (semana 26)
    int HembrasIniciales,
    int MachosIniciales,
    int HuevosIniciales,
    
    // Resumen por Etapas
    EtapaLiquidacionDto Etapa1, // Semanas 25-33
    EtapaLiquidacionDto Etapa2, // Semanas 34-50
    EtapaLiquidacionDto Etapa3, // Semanas >50
    
    // Totales acumulados (desde semana 26)
    MetricasAcumuladasProduccionDto Totales,
    
    // Comparación con Guía Genética
    ComparacionGuiaProduccionDto? ComparacionGuia,
    
    // Metadatos
    DateTime FechaCalculo,
    int TotalRegistrosSeguimiento,
    DateTime? FechaUltimoSeguimiento,
    int SemanaActual
);

/// <summary>
/// DTO para métricas de una etapa específica
/// </summary>
public record EtapaLiquidacionDto(
    int Etapa,
    string Nombre, // "Etapa 1 (25-33)", "Etapa 2 (34-50)", "Etapa 3 (>50)"
    int SemanaDesde,
    int? SemanaHasta,
    int TotalRegistros,
    
    // Mortalidad
    int MortalidadHembras,
    int MortalidadMachos,
    decimal PorcentajeMortalidadHembras,
    decimal PorcentajeMortalidadMachos,
    
    // Selección (retiradas)
    int SeleccionHembras,
    decimal PorcentajeSeleccionHembras,
    
    // Consumo
    decimal ConsumoKgHembras,
    decimal ConsumoKgMachos,
    decimal ConsumoTotalKg,
    
    // Producción de Huevos
    int HuevosTotales,
    int HuevosIncubables,
    decimal PromedioHuevosPorDia,
    decimal EficienciaProduccion, // %
    
    // Peso (último registro de la etapa)
    decimal? PesoHembras,
    decimal? PesoMachos,
    decimal? Uniformidad,
    
    // Clasificadora de Huevos (totales de la etapa)
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
    
    // Pesaje Semanal (promedio si hay datos)
    decimal? PesoPromedioHembras,
    decimal? PesoPromedioMachos,
    decimal? UniformidadPromedio,
    decimal? CoeficienteVariacionPromedio
);

/// <summary>
/// DTO para métricas acumuladas desde semana 26
/// </summary>
public record MetricasAcumuladasProduccionDto(
    // Mortalidad acumulada
    int TotalMortalidadHembras,
    int TotalMortalidadMachos,
    decimal PorcentajeMortalidadAcumuladaHembras,
    decimal PorcentajeMortalidadAcumuladaMachos,
    
    // Selección acumulada
    int TotalSeleccionHembras,
    decimal PorcentajeSeleccionAcumuladaHembras,
    
    // Consumo acumulado
    decimal ConsumoTotalKgHembras,
    decimal ConsumoTotalKgMachos,
    decimal ConsumoTotalKg,
    decimal ConsumoPromedioDiarioKg,
    
    // Producción de Huevos acumulada
    int TotalHuevosTotales,
    int TotalHuevosIncubables,
    decimal PromedioHuevosPorDia,
    decimal EficienciaProduccionTotal, // %
    
    // Aves actuales (iniciales - mortalidad - selección)
    int AvesHembrasActuales,
    int AvesMachosActuales,
    int TotalAvesActuales
);

/// <summary>
/// DTO para comparación con guía genética
/// Incluye todos los campos disponibles de la guía genética para comparación
/// </summary>
public record ComparacionGuiaProduccionDto(
    // Consumo
    decimal? ConsumoGuiaHembras,
    decimal? ConsumoGuiaMachos,
    decimal? DiferenciaConsumoHembras, // %
    decimal? DiferenciaConsumoMachos, // %
    
    // Peso
    decimal? PesoGuiaHembras,
    decimal? PesoGuiaMachos,
    decimal? DiferenciaPesoHembras, // %
    decimal? DiferenciaPesoMachos, // %
    
    // Mortalidad
    decimal? MortalidadGuiaHembras,
    decimal? MortalidadGuiaMachos,
    decimal? DiferenciaMortalidadHembras, // %
    decimal? DiferenciaMortalidadMachos, // %
    
    // Uniformidad
    decimal? UniformidadGuia,
    decimal? UniformidadReal,
    decimal? DiferenciaUniformidad, // %
    
    // Producción de Huevos (Guía Genética)
    decimal? HuevosTotalesGuia, // HTotalAa
    decimal? PorcentajeProduccionGuia, // ProdPorcentaje
    decimal? HuevosIncubablesGuia, // HIncAa
    decimal? PesoHuevoGuia, // PesoHuevo (gramos)
    decimal? MasaHuevoGuia, // MasaHuevo (gramos)
    decimal? GramosHuevoTotalGuia, // GrHuevoT
    decimal? GramosHuevoIncubableGuia, // GrHuevoInc
    decimal? AprovechamientoSemanalGuia, // AprovSem (%)
    decimal? AprovechamientoAcumuladoGuia, // AprovAc (%)
    
    // Producción de Huevos (Real - Promedio)
    decimal? HuevosTotalesReal,
    decimal? PorcentajeProduccionReal,
    decimal? HuevosIncubablesReal,
    decimal? PesoHuevoReal,
    decimal? EficienciaReal,
    
    // Diferencias de Producción
    decimal? DiferenciaHuevosTotales, // %
    decimal? DiferenciaPorcentajeProduccion, // %
    decimal? DiferenciaHuevosIncubables, // %
    decimal? DiferenciaPesoHuevo, // %
    decimal? DiferenciaMasaHuevo, // %
    
    // Datos adicionales de guía genética
    decimal? NacimientoPorcentajeGuia, // NacimPorcentaje
    decimal? PollitosAveAlojadaGuia, // PollitoAa
    decimal? GramosPollitoGuia, // GrPollito
    decimal? ApareoGuia, // Apareo (%)
    decimal? KcalAveDiaHGuia, // KcalAveDiaH
    decimal? KcalAveDiaMGuia, // KcalAveDiaM
    
    // Retiro acumulado de guía
    decimal? RetiroAcumuladoHembrasGuia, // RetiroAcH
    decimal? RetiroAcumuladoMachosGuia // RetiroAcM
);

/// <summary>
/// Request para calcular liquidación técnica de producción
/// </summary>
public record LiquidacionTecnicaProduccionRequest(
    int LoteId,
    DateTime? FechaHasta = null, // Si no se especifica, usa la fecha actual
    int? EtapaFiltro = null // Opcional: filtrar solo una etapa (1, 2 o 3)
);

