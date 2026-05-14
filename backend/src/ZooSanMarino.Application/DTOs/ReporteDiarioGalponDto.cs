// src/ZooSanMarino.Application/DTOs/ReporteDiarioGalponDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Fila diaria de reporte de producción para un galpón específico.
/// Incluye datos reales + valores guía STANDARD + diferencias.
/// </summary>
public record ReporteDiarioGalponDto(
    int    LotePosturaProduccionId,
    string GalponId,
    string GalponNombre,
    string LoteNombre,
    DateTime Fecha,
    int    SemanaRelativa,
    int    EdadDias,
    // Saldo
    int SaldoHembras,
    int SaldoMachos,
    // Mortalidad
    int    MortalidadHembras,
    int    MortalidadMachos,
    double PorcMortalidad,
    // Consumo
    double ConsKgH,
    double ConsKgM,
    // Huevos
    int    HuevoTot,
    int    HuevoInc,
    double PorcentajePostura,
    double PorcentajeIncubables,
    // Peso
    double  PesoHuevo,
    double? PesoH,
    double? PesoM,
    // Calidad
    double? Uniformidad,
    double? Htaa,
    // GUIA (tabla STANDARD / ProduccionAvicolaRaw)
    double? PorcentajePosturaGuia,
    double? PesoHuevoGuia,
    double? HtaaGuia,
    double? UniformidadGuia,
    // Diferencias Real − Guía
    double? DifPostura,
    double? DifPesoHuevo,
    string? Observaciones
);
