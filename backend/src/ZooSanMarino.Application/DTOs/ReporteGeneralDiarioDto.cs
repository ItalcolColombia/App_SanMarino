// src/ZooSanMarino.Application/DTOs/ReporteGeneralDiarioDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Fila diaria consolidada (todos los galpones sumados) del reporte general de producción.
/// </summary>
public record ReporteGeneralDiarioDto(
    DateTime Fecha,
    int    SemanaRelativa,
    int    EdadDias,
    // Saldo consolidado
    int SaldoTotalHembras,
    int SaldoTotalMachos,
    // Mortalidad
    int    MortalidadTotalHembras,
    int    MortalidadTotalMachos,
    double PorcMortalidadPromedio,
    // Consumo
    double ConsKgHTotalKg,
    double ConsKgMTotalKg,
    // Huevos
    int    HuevosTotTotal,
    int    HuevosIncTotal,
    double PorcentajePosturaPromedio,
    double PesoHuevoPromedio,
    double? UniformidadPromedio,
    // GUIA
    double? PorcentajePosturaGuia,
    double? PesoHuevoGuia,
    double? HtaaGuia,
    // Diferencia
    double? DifPostura
);
