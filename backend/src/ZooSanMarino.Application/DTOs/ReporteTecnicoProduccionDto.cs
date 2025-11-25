// src/ZooSanMarino.Application/DTOs/ReporteTecnicoProduccionDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para reporte técnico diario de producción
/// </summary>
public record ReporteTecnicoProduccionDiarioDto(
    int Dia,
    int Semana,
    DateTime Fecha,
    int MortalidadHembras,
    int MortalidadMachos,
    int SeleccionHembras, // Aves eliminadas por no conformidad/descartes
    int SeleccionMachos,
    int VentasHembras, // Aves vendidas
    int VentasMachos,
    int TrasladosHembras, // Aves trasladadas
    int TrasladosMachos,
    int SaldoHembras, // Aves actuales
    int SaldoMachos,
    int HuevosTotales, // Postura
    decimal PorcentajePostura, // % de postura
    decimal KilosAlimentoHembras,
    decimal KilosAlimentoMachos,
    int HuevosEnviadosPlanta, // Huevos enviados a planta
    decimal PorcentajeIncubable, // % de huevos incubables
    decimal? PesoHembra, // Peso promedio hembras (kg)
    decimal? PesoMachos, // Peso promedio machos (kg)
    decimal PesoHuevo // Peso promedio del huevo (g)
);

/// <summary>
/// DTO para reporte técnico semanal de producción
/// </summary>
public record ReporteTecnicoProduccionSemanalDto(
    int Semana,
    DateTime FechaInicioSemana,
    DateTime FechaFinSemana,
    int EdadInicioSemanas,
    int EdadFinSemanas,
    int MortalidadHembrasSemanal,
    int MortalidadMachosSemanal,
    int SeleccionHembrasSemanal,
    int SeleccionMachosSemanal,
    int VentasHembrasSemanal,
    int VentasMachosSemanal,
    int TrasladosHembrasSemanal,
    int TrasladosMachosSemanal,
    int SaldoInicioHembras,
    int SaldoFinHembras,
    int SaldoInicioMachos,
    int SaldoFinMachos,
    int HuevosTotalesSemanal,
    decimal PorcentajePosturaPromedio,
    decimal KilosAlimentoHembrasSemanal,
    decimal KilosAlimentoMachosSemanal,
    int HuevosEnviadosPlantaSemanal,
    decimal PorcentajeIncubablePromedio,
    decimal? PesoHembraPromedio,
    decimal? PesoMachosPromedio,
    decimal PesoHuevoPromedio,
    List<ReporteTecnicoProduccionDiarioDto> DetalleDiario
);

/// <summary>
/// DTO con información del lote para el reporte
/// </summary>
public record ReporteTecnicoProduccionLoteInfoDto(
    int LoteId,
    string LoteNombre,
    string? Raza,
    string? Linea,
    DateTime? FechaInicioProduccion,
    int? NumeroHembrasIniciales,
    int? NumeroMachosIniciales,
    int? Galpon,
    string? Tecnico,
    string? GranjaNombre,
    string? NucleoNombre
);

/// <summary>
/// DTO completo del reporte técnico de producción
/// </summary>
public record ReporteTecnicoProduccionCompletoDto(
    ReporteTecnicoProduccionLoteInfoDto LoteInfo,
    List<ReporteTecnicoProduccionDiarioDto> DatosDiarios,
    List<ReporteTecnicoProduccionSemanalDto> DatosSemanales
);

/// <summary>
/// DTO para solicitar generación de reporte técnico de producción
/// </summary>
public record GenerarReporteTecnicoProduccionRequestDto(
    string TipoReporte, // "diario" o "semanal"
    string TipoConsolidacion, // "sublote" o "consolidado"
    int? LoteId, // Opcional para sublote
    string? LoteNombreBase, // Opcional para consolidado
    DateTime? FechaInicio, // Opcional para diario
    DateTime? FechaFin, // Opcional para diario
    int? Semana // Opcional para semanal
);

