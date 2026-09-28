namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Movimiento de alimento visible en el seguimiento diario de postura. La fuente es
/// <c>lote_registro_historico_unificado</c>; no duplica ni modifica inventario.
/// </summary>
public sealed record MovimientoAlimentoSeguimientoDto(
    long Id,
    DateTime Fecha,
    string TipoMovimiento,
    decimal CantidadKg,
    string? Alimento,
    string? Referencia,
    string? NumeroDocumento);

/// <summary>
/// Movimientos ya clasificados y agrupados por día por PostgreSQL. El backend sólo deserializa el
/// JSON retornado por <c>fn_movimientos_alimento_postura_diario</c>.
/// </summary>
public sealed record ResumenMovimientosAlimentoDiaDto(
    DateTime Fecha,
    IReadOnlyList<MovimientoAlimentoSeguimientoDto> Ingresos,
    IReadOnlyList<MovimientoAlimentoSeguimientoDto> Traslados,
    IReadOnlyList<string> Referencias);
