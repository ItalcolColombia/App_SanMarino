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
    decimal PesoHuevo, // Peso promedio del huevo (g)
    // Desglose de tipos de huevos
    int HuevoLimpio,
    int HuevoTratado,
    int HuevoSucio,
    int HuevoDeforme,
    int HuevoBlanco,
    int HuevoDobleYema,
    int HuevoPiso,
    int HuevoPequeno,
    int HuevoRoto,
    int HuevoDesecho,
    int HuevoOtro,
    // Porcentajes de tipos de huevos
    decimal? PorcentajeLimpio,
    decimal? PorcentajeTratado,
    decimal? PorcentajeSucio,
    decimal? PorcentajeDeforme,
    decimal? PorcentajeBlanco,
    decimal? PorcentajeDobleYema,
    decimal? PorcentajePiso,
    decimal? PorcentajePequeno,
    decimal? PorcentajeRoto,
    decimal? PorcentajeDesecho,
    decimal? PorcentajeOtro,
    // Transferencias de huevos del día
    int HuevosTrasladadosTotal,
    int HuevosTrasladadosLimpio,
    int HuevosTrasladadosTratado,
    int HuevosTrasladadosSucio,
    int HuevosTrasladadosDeforme,
    int HuevosTrasladadosBlanco,
    int HuevosTrasladadosDobleYema,
    int HuevosTrasladadosPiso,
    int HuevosTrasladadosPequeno,
    int HuevosTrasladadosRoto,
    int HuevosTrasladadosDesecho,
    int HuevosTrasladadosOtro
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
    // Desglose de tipos de huevos semanal
    int HuevoLimpioSemanal,
    int HuevoTratadoSemanal,
    int HuevoSucioSemanal,
    int HuevoDeformeSemanal,
    int HuevoBlancoSemanal,
    int HuevoDobleYemaSemanal,
    int HuevoPisoSemanal,
    int HuevoPequenoSemanal,
    int HuevoRotoSemanal,
    int HuevoDesechoSemanal,
    int HuevoOtroSemanal,
    // Porcentajes promedio de tipos de huevos
    decimal? PorcentajeLimpioPromedio,
    decimal? PorcentajeTratadoPromedio,
    decimal? PorcentajeSucioPromedio,
    decimal? PorcentajeDeformePromedio,
    decimal? PorcentajeBlancoPromedio,
    decimal? PorcentajeDobleYemaPromedio,
    decimal? PorcentajePisoPromedio,
    decimal? PorcentajePequenoPromedio,
    decimal? PorcentajeRotoPromedio,
    decimal? PorcentajeDesechoPromedio,
    decimal? PorcentajeOtroPromedio,
    // Transferencias de huevos semanal
    int HuevosTrasladadosTotalSemanal,
    int HuevosTrasladadosLimpioSemanal,
    int HuevosTrasladadosTratadoSemanal,
    int HuevosTrasladadosSucioSemanal,
    int HuevosTrasladadosDeformeSemanal,
    int HuevosTrasladadosBlancoSemanal,
    int HuevosTrasladadosDobleYemaSemanal,
    int HuevosTrasladadosPisoSemanal,
    int HuevosTrasladadosPequenoSemanal,
    int HuevosTrasladadosRotoSemanal,
    int HuevosTrasladadosDesechoSemanal,
    int HuevosTrasladadosOtroSemanal,
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

