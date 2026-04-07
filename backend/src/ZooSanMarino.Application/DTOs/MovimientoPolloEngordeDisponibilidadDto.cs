namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Disponibilidad calculada para venta (incluye reservas Pendiente para evitar sobreventa).
/// </summary>
public sealed record AvesDisponiblesVentaLoteDto(
    int LoteId,
    string TipoLote,
    string? NombreLote,
    int HembrasDisponibles,
    int MachosDisponibles,
    int MixtasDisponibles,
    int TotalDisponibles,
    int HembrasReservadasPendiente,
    int MachosReservadasPendiente,
    int MixtasReservadasPendiente,
    int TotalReservadasPendiente
);

public sealed class AvesDisponiblesLotesRequest
{
    /// <summary>"LoteAveEngorde" | "LoteReproductoraAveEngorde"</summary>
    public string TipoLote { get; set; } = "LoteAveEngorde";
    public List<int> LoteIds { get; set; } = new();
}

public sealed record AvesDisponiblesLotePorIdDto(int LoteId, AvesDisponiblesVentaLoteDto? Disponibles);

public sealed class AvesDisponiblesLotesResponse
{
    public List<AvesDisponiblesLotePorIdDto> Items { get; set; } = new();
}

