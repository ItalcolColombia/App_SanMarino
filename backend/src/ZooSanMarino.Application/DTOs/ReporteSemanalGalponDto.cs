// src/ZooSanMarino.Application/DTOs/ReporteSemanalGalponDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Fila semanal de reporte de producción para un galpón específico.
/// Incluye datos reales agregados por semana + valores guía STANDARD.
/// </summary>
public record ReporteSemanalGalponDto(
    int    LotePosturaProduccionId,
    string GalponId,
    string GalponNombre,
    string LoteNombre,
    int    Semana,
    DateTime FechaInicioSemana,
    DateTime FechaFinSemana,
    int EdadSemanas,
    // Saldo
    int SaldoInicioHembras,
    int SaldoInicioMachos,
    int SaldoFinHembras,
    int SaldoFinMachos,
    // Mortalidad
    int    MortalidadHembrasSemanal,
    int    MortalidadMachosSemanal,
    double PorcMortalidadSemanal,
    // Consumo
    double ConsKgHSemanal,
    double ConsKgMSemanal,
    // Huevos
    int    HuevoTotSemanal,
    int    HuevoIncSemanal,
    double PorcentajePosturaPromedio,
    double PorcentajeIncubablesPromedio,
    // Peso
    double  PesoHuevoPromedio,
    double? PesoHPromedio,
    double? PesoMPromedio,
    // Calidad
    double? UniformidadPromedio,
    double? HtaaSemanal,
    // GUIA
    double? PorcentajePosturaGuia,
    double? PesoHuevoGuia,
    double? HtaaGuia,
    double? UniformidadGuia,
    // Diferencias
    double? DifPostura,
    double? DifPesoHuevo
);
