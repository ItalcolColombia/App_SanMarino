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
    decimal ConsumoAlimentoKgM
);

public sealed record InformacionLoteResponse(
    InformacionLoteDto InformacionLote
);

