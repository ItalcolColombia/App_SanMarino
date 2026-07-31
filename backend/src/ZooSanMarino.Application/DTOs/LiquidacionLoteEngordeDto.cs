namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Resumen para el modal "Liquidar lote" (pollo de engorde).
/// <para>
/// Con el lote liquidado, el resumen sale de la COPIA CONGELADA
/// (<c>liquidacion_lote_engorde_congelada</c>) y <see cref="CongeladaAt"/>/<see cref="FnVersion"/>
/// vienen pobladas — el front las usa para el badge «datos congelados». En vivo llegan null.
/// </para>
/// </summary>
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
    decimal? SaldoAlimentoKg,
    int? MermaUnidades = null,
    decimal? MermaKilos = null,
    DateTime? CongeladaAt = null,
    string? FnVersion = null
);

public sealed record CerrarLoteAveEngordeRequest(
    string ClosedByUserId,
    int? MermaUnidades = null,
    decimal? MermaKilos = null,
    /// <summary>Fecha de liquidación elegida por el usuario (Ecuador). Si es null, se usa el momento actual (comportamiento previo).</summary>
    DateTime? FechaLiquidacion = null);

public sealed record AbrirLoteAveEngordeRequest(string Motivo, string OpenedByUserId);

/// <summary>Digitación/edición de la merma por Costos (lote abierto o cerrado). Parte B / R1.</summary>
public sealed record ActualizarMermaLoteEngordeRequest(
    int? MermaUnidades,
    decimal? MermaKilos,
    string RegistradoPorUserId);
