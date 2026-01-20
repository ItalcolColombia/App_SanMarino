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
    decimal PorcentajeEnviadoPlanta, // % de huevos enviados a planta
    int HuevosIncubables, // Huevos incubables (HuevoInc)
    int HuevosCargados, // Huevos cargados en incubadora (puede ser igual a Incubables)
    decimal? PorcentajeNacimientos, // % de pollitos nacidos (calculado)
    int? VentaHuevo, // Cantidad de huevos vendidos
    int? PollitosVendidos, // Pollitos hembras vendidos
    decimal? PesoHembra, // Peso promedio hembras (kg)
    decimal? PesoMachos, // Peso promedio machos (kg)
    decimal PesoHuevo, // Peso promedio del huevo (g)
    decimal? PorcentajeGrasaCorporal, // % de grasa corporal
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
    decimal PorcentajeEnviadoPlantaPromedio,
    int HuevosIncubablesSemanal,
    int HuevosCargadosSemanal,
    decimal? PorcentajeNacimientosPromedio,
    int? VentaHuevoSemanal,
    int? PollitosVendidosSemanal,
    decimal? PesoHembraPromedio,
    decimal? PesoMachosPromedio,
    decimal PesoHuevoPromedio,
    decimal? PorcentajeGrasaCorporalPromedio,
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
/// DTO para reporte técnico "Cuadro" semanal con valores de guía genética (amarillos)
/// </summary>
public record ReporteTecnicoProduccionCuadroDto(
    int Semana,
    DateTime Fecha,
    int EdadProduccionSemanas, // ED PrH - Edad en semanas de producción
    int AvesFinHembras,
    int AvesFinMachos,
    // MORTALIDAD HEMBRAS
    int MortalidadHembrasN,
    decimal MortalidadHembrasDescPorcentajeSem,
    decimal MortalidadHembrasPorcentajeAcum,
    decimal? MortalidadHembrasStandarM, // AMARILLO - Guía genética
    decimal? MortalidadHembrasAcumStandar, // AMARILLO - Guía genética acumulada
    // MORTALIDAD MACHOS
    int MortalidadMachosN,
    decimal MortalidadMachosDescPorcentajeSem,
    decimal MortalidadMachosPorcentajeAcum,
    decimal? MortalidadMachosStandarM, // AMARILLO - Guía genética
    decimal? MortalidadMachosAcumStandar, // AMARILLO - Guía genética acumulada
    // PRODUCCION TOTAL DE HUEVOS
    int HuevosVentaSemana,
    int HuevosAcum,
    decimal PorcentajeSem,
    decimal? PorcentajeRoss, // AMARILLO - % ROSS
    decimal Taa, // Technical Accumulated Average
    decimal? TaaRoss, // AMARILLO - TAA ROSS
    // HUEVOS ENVIADOS PLANTA
    int EnviadosPlanta,
    int AcumEnviaP,
    decimal PorcentajeEnviaP,
    decimal? PorcentajeHala, // AMARILLO - % HALA
    // HUEVO INCUBABLE
    int HuevosIncub,
    decimal PorcentajeDescarte,
    decimal PorcentajeAcumIncub,
    decimal Laa, // Liveability Accumulated Average
    decimal? StdRoss, // AMARILLO - STD ROSS
    // HUEVOS CARGADOS Y POLLITOS
    int HCarga,
    int HCargaAcu,
    int VHuevo,
    int VHuevoPollitos,
    int PollAcum,
    decimal Paa, // Performance Accumulated Average
    decimal? PaaRoss, // AMARILLO - PAA ROSS
    // CONSUMO DE ALIMENTO HEMBRA
    decimal KgSemHembra,
    decimal AcumHembra,
    decimal AcumAaHembra, // Accumulated Average
    decimal? StAcumHembra, // AMARILLO - Standard Accumulated
    decimal? LoteHembra, // Valor del lote
    decimal? StGrHembra, // AMARILLO - Standard Gram/Day
    // CONSUMO DE ALIMENTO MACHO
    decimal KgSemMachos,
    decimal AcumMachos,
    decimal AcumAaMachos, // Accumulated Average
    decimal? StAcumMachos, // AMARILLO - Standard Accumulated
    decimal GrDiaMachos, // Gram/Day
    decimal? StGrMachos, // AMARILLO - Standard Gram/Day
    // PESOS
    decimal? PesoHembraKg,
    decimal? PesoHembraStd, // AMARILLO - Standard
    decimal? PesoMachosKg,
    decimal? PesoMachosStd, // AMARILLO - Standard
    decimal PesoHuevoSem,
    decimal? PesoHuevoStd, // AMARILLO - Standard
    decimal MasaSem,
    decimal? MasaStd, // AMARILLO - Standard
    // % APROV (Aprovechamiento)
    decimal? PorcentajeAprovSem,
    decimal? PorcentajeAprovStd, // AMARILLO - Standard
    // TIPO DE ALIMENTO
    string? TipoAlimento,
    // OBSERVACIONES
    string? Observaciones
);

/// <summary>
/// DTO completo del reporte "Cuadro" de producción
/// </summary>
public record ReporteTecnicoProduccionCuadroCompletoDto(
    ReporteTecnicoProduccionLoteInfoDto LoteInfo,
    List<ReporteTecnicoProduccionCuadroDto> DatosCuadro
);

/// <summary>
/// DTO para reporte de clasificación de huevos comercio semanal
/// </summary>
public record ReporteClasificacionHuevoComercioDto(
    int Semana,
    DateTime FechaInicioSemana,
    DateTime FechaFinSemana,
    string LoteNombre,
    // Datos reales
    int IncubableLimpio, // INCUBABLE LIMPIO
    int HuevoTratado, // HUEVO TRATADO
    decimal PorcentajeTratado, // % TRATADO
    int HuevoDY, // HUEVO DY (Doble Yema)
    decimal PorcentajeDY, // % DY
    int HuevoRoto, // HUEVO ROTO
    decimal PorcentajeRoto, // % ROTO
    int HuevoDeforme, // HUEVO DEFORME
    decimal PorcentajeDeforme, // % DEFORME
    int HuevoPiso, // HUEVO PISO
    decimal PorcentajePiso, // % PISO
    int HuevoDesecho, // HUEVO DESECHO
    decimal PorcentajeDesecho, // % DESECHO
    int HuevoPIP, // HUEVO PIP (pequeño)
    decimal PorcentajePIP, // % PIP
    int HuevoSucioDeBanda, // HUEVO SUCIO DE BANDA
    decimal PorcentajeSucioDeBanda, // % SUCIO DE BANDA
    int TotalPN, // TOTAL PN (Producción Neta)
    decimal PorcentajeTotal, // % del total
    // Valores de guía genética (amarillos)
    int? IncubableLimpioGuia, // AMARILLO
    int? HuevoTratadoGuia, // AMARILLO
    decimal? PorcentajeTratadoGuia, // AMARILLO
    int? HuevoDYGuia, // AMARILLO
    decimal? PorcentajeDYGuia, // AMARILLO
    int? HuevoRotoGuia, // AMARILLO
    decimal? PorcentajeRotoGuia, // AMARILLO
    int? HuevoDeformeGuia, // AMARILLO
    decimal? PorcentajeDeformeGuia, // AMARILLO
    int? HuevoPisoGuia, // AMARILLO
    decimal? PorcentajePisoGuia, // AMARILLO
    int? HuevoDesechoGuia, // AMARILLO
    decimal? PorcentajeDesechoGuia, // AMARILLO
    int? HuevoPIPGuia, // AMARILLO
    decimal? PorcentajePIPGuia, // AMARILLO
    int? HuevoSucioDeBandaGuia, // AMARILLO
    decimal? PorcentajeSucioDeBandaGuia, // AMARILLO
    int? TotalPNGuia, // AMARILLO
    decimal? PorcentajeTotalGuia // AMARILLO
);

/// <summary>
/// DTO completo del reporte de clasificación de huevos comercio
/// </summary>
public record ReporteClasificacionHuevoComercioCompletoDto(
    ReporteTecnicoProduccionLoteInfoDto LoteInfo,
    List<ReporteClasificacionHuevoComercioDto> DatosClasificacion
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

