namespace ZooSanMarino.Application.DTOs;

public sealed record SeguimientoAvesEngordeBackfillResultDto(
    int LoteId,
    DateTime? Desde,
    DateTime? Hasta,
    int TotalRegistros,
    int Actualizados,
    int Omitidos,
    int SinDatosHistorico);

