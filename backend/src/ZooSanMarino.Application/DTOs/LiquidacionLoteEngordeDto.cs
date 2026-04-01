namespace ZooSanMarino.Application.DTOs;

/// <summary>Resumen para el modal "Liquidar lote" (pollo de engorde).</summary>
public sealed record LiquidacionLoteEngordeResumenDto(
    int LoteAveEngordeId,
    string LoteNombre,
    string EstadoOperativoLote,
    int? HembrasInicio,
    int? MachosInicio,
    int? MixtasInicio,
    int TotalAvesInicio,
    int VentasTotalHembras,
    int VentasTotalMachos,
    int VentasTotalMixtas,
    int AvesVivasActuales,
    int MovimientosVentaCount,
    decimal? SaldoAlimentoKg
);

public sealed record CerrarLoteAveEngordeRequest(string ClosedByUserId);

public sealed record AbrirLoteAveEngordeRequest(string Motivo, string OpenedByUserId);
