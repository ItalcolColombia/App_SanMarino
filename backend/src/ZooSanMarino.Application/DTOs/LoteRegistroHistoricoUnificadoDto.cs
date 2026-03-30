namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Fila de lote_registro_historico_unificado (inventario EC + ventas aves) para la UI de seguimiento.
/// </summary>
public sealed record LoteRegistroHistoricoUnificadoDto(
    long Id,
    int CompanyId,
    int? LoteAveEngordeId,
    int FarmId,
    string? NucleoId,
    string? GalponId,
    DateTime FechaOperacion,
    string TipoEvento,
    string OrigenTabla,
    int OrigenId,
    string? MovementTypeOriginal,
    int? ItemInventarioEcuadorId,
    string? ItemResumen,
    decimal? CantidadKg,
    string? Unidad,
    int? CantidadHembras,
    int? CantidadMachos,
    int? CantidadMixtas,
    string? Referencia,
    string? NumeroDocumento,
    decimal? AcumuladoEntradasAlimentoKg,
    bool Anulado,
    DateTimeOffset CreatedAt);
