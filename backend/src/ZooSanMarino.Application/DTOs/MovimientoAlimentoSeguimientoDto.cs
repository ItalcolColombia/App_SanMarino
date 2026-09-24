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
