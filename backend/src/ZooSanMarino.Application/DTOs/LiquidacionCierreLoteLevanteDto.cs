namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Variables de resultado para cierre / liquidación técnica del lote de levante (semana 25).
/// </summary>
public record LiquidacionCierreLoteLevanteDto(
    // Identificación
    int LotePosturaLevanteId,
    string LoteNombre,
    string? Raza,
    int? AnoTablaGenetica,

    // Aves encasetadas
    int? HembrasEncasetadas,
    int? MachosEncasetados,

    // % Retiro acumulado hembras
    decimal PorcentajeMortalidadHembras,
    decimal PorcentajeSeleccionHembras,
    decimal PorcentajeErrorSexajeHembras,
    decimal PorcentajeRetiroAcumulado,        // Mort + Sel + Error
    decimal? PorcentajeRetiroGuia,            // RetiroAcH de la guía

    // Consumo alimento acumulado (g/ave total semanas 1-25)
    decimal ConsumoAlimentoRealGramos,
    decimal? ConsumoAlimentoGuiaGramos,       // ConsAcH de la guía
    decimal? PorcentajeDiferenciaConsumo,

    // Consumo por ave por día en semana 25 (sólo guía — GrAveDiaH)
    decimal? ConsumoGrAveDiaSemana25Guia,

    // Peso semana 25
    decimal? PesoSemana25Real,
    decimal? PesoSemana25Guia,               // PesoH de la guía
    decimal? PorcentajeDiferenciaPeso,

    // Uniformidad
    decimal? UniformidadReal,
    decimal? UniformidadGuia,                // Uniformidad de la guía
    decimal? PorcentajeDiferenciaUniformidad,

    // Metadatos
    DateTime FechaCalculo,
    int TotalRegistrosSeguimiento,
    int? SemanaUltimoRegistro,
    bool TieneGuiaGenetica
);

/// <summary>Request para guardar la liquidación al cerrar el lote.</summary>
public record GuardarLiquidacionCierreLoteLevanteRequest(
    int LotePosturaLevanteId
);

/// <summary>Respuesta al guardar la liquidación.</summary>
public record LiquidacionCierreGuardadaDto(
    int Id,
    int LotePosturaLevanteId,
    DateTime FechaCierre,
    int? ClosedByUserId,
    LiquidacionCierreLoteLevanteDto Datos
);
