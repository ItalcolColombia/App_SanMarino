// src/ZooSanMarino.Application/DTOs/ReporteGeneralSemanalDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Fila semanal consolidada (todos los galpones sumados) del reporte general de producción.
/// </summary>
public record ReporteGeneralSemanalDto(
    int      Semana,
    DateTime FechaInicioSemana,
    DateTime FechaFinSemana,
    int EdadSemanas,
    // Saldo consolidado
    int SaldoInicioHembras,
    int SaldoInicioMachos,
    int SaldoFinHembras,
    int SaldoFinMachos,
    // Mortalidad
    int    MortalidadTotalHembras,
    int    MortalidadTotalMachos,
    double PorcMortalidadSemanal,
    // Consumo
    double ConsKgHTotal,
    double ConsKgMTotal,
    // Huevos
    int    HuevosTotTotal,
    int    HuevosIncTotal,
    double PorcentajePosturaPromedio,
    double PesoHuevoPromedio,
    double? PesoHPromedio,
    double? PesoMPromedio,
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
