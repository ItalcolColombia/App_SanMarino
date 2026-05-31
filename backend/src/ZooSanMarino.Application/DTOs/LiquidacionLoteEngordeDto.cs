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

public sealed record CerrarLoteAveEngordeRequest(
    string ClosedByUserId,
    int? MermaUnidades = null,
    decimal? MermaKilos = null);

public sealed record AbrirLoteAveEngordeRequest(string Motivo, string OpenedByUserId);

/// <summary>Digitación/edición de la merma por Costos (lote abierto o cerrado). Parte B / R1.</summary>
public sealed record ActualizarMermaLoteEngordeRequest(
    int? MermaUnidades,
    decimal? MermaKilos,
    string RegistradoPorUserId);
