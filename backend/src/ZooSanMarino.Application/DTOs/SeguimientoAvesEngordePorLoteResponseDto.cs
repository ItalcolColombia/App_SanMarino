namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Respuesta unificada de GET SeguimientoAvesEngorde/por-lote/{loteId}:
/// registros diarios del lote + historial unificado (inventario EC y ventas de aves).
/// </summary>
/// <remarks>
/// Mapeo orientativo para la tabla principal (pestaña Seguimiento):
/// consumo de alimento (kg/día) en el seguimiento diario; en historial, INV_CONSUMO aporta cantidad kg por ítem.
/// Ingreso/traslado: tipos INV_INGRESO, INV_TRASLADO_ENTRADA, INV_TRASLADO_SALIDA (cantidad kg).
/// Documento o referencia: NumeroDocumento / Referencia en historial.
/// Despacho hembras y machos: tipo VENTA_AVES (CantidadHembras, CantidadMachos, CantidadMixtas).
/// Acumulado entradas de alimento: AcumuladoEntradasAlimentoKg en filas de ingreso o traslado entrada.
/// </remarks>
public sealed record SeguimientoAvesEngordePorLoteResponseDto(
    IReadOnlyList<SeguimientoLoteLevanteDto> Seguimientos,
    IReadOnlyList<LoteRegistroHistoricoUnificadoDto> HistoricoUnificado);
