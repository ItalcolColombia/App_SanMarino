namespace ZooSanMarino.Application.DTOs.Produccion;

public sealed record InformacionLoteDto(
    int LotePosturaProduccionId,
    string LoteNombre,
    string Estado,
    DateTime? FechaEncaset,
    DateTime? FechaInicioProduccion,
    int AvesInicialesH,
    int AvesInicialesM,
    int AvesActualesH,
    int AvesActualesM,
    int EdadSemanasProduccion,
    int Registros,
    int MortalidadSeleccionH,
    int MortalidadSeleccionM,
    decimal ConsumoAlimentoKgH,
    decimal ConsumoAlimentoKgM,
    // Feature 14 — acumulados de traslado por fase del lote
    int LevanteTrasladoIngresoHembras = 0,
    int LevanteTrasladoIngresoMachos = 0,
    int LevanteTrasladoSalidaHembras = 0,
    int LevanteTrasladoSalidaMachos = 0,
    int ProduccionTrasladoIngresoHembras = 0,
    int ProduccionTrasladoIngresoMachos = 0,
    int ProduccionTrasladoSalidaHembras = 0,
    int ProduccionTrasladoSalidaMachos = 0
);

public sealed record InformacionLoteResponse(
    InformacionLoteDto InformacionLote
);

