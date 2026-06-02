namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Reporte de liquidación técnica de un lote en Panamá ("RESULTADOS DE LIQUIDACIÓN").
/// Generado por la función SQL fn_reporte_indicadores_panama a partir de los insumos
/// digitados por el usuario + agregados del seguimiento diario Panamá.
/// </summary>
public sealed record ReporteIndicadoresPanamaDto(
    LiquidacionPanamaDto Liquidacion,
    InfoProductivaPanamaDto InfoProductiva,
    int AvesEncasetadas
);

/// <summary>Insumos + indicadores derivados de la liquidación Panamá.</summary>
public sealed record LiquidacionPanamaDto(
    int Id,
    string? IdUsuarioRegistro,
    int IdLote,
    // ── Insumos digitados ──
    decimal MetrosCuadrados,
    decimal AvesFinalGranja,
    decimal ProduccionKiloPie,
    int DiasEngorde,
    int DiasEnGranja,
    int AvesBeneficiada,
    // ── Derivados (calculados por la fn) ──
    decimal PesoPromedio,
    decimal MortalidadPorc,
    decimal SeleccionPorc,
    decimal PorcMortalidadTotal,
    decimal Supervivencia,
    decimal ConsumoAve,
    decimal Conversion,
    decimal EficienciaAmericana,
    decimal EeF,
    decimal EefDos,
    decimal AvesMetrosCua,
    decimal KilosMetrosCua,
    decimal Productividad,
    decimal FaltanteSobra
);

/// <summary>Agregados productivos tomados del seguimiento diario Panamá.</summary>
public sealed record InfoProductivaPanamaDto(
    decimal ConsumoAlimentoTotal,
    decimal TotalAvesSeleccion,
    decimal TotalAvesMuertas
);

/// <summary>Payload para guardar/actualizar los 6 insumos de liquidación Panamá.</summary>
public sealed record GuardarLiquidacionPanamaRequest(
    int LoteAveEngordeId,
    decimal MetrosCuadrados,
    int AvesFinalGranja,
    int AvesBeneficiada,
    decimal ProduccionKiloPie,
    int DiasEngorde,
    int DiasEnGranja,
    string? RegistradoPorUserId
);
