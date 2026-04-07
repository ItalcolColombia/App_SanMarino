namespace ZooSanMarino.Application.DTOs;

public sealed class CorregirVentasCompletadasRequest
{
    public int? GranjaId { get; set; }
    public List<int>? LoteAveEngordeIds { get; set; }
    /// <summary>Si true, simula cambios y no persiste.</summary>
    public bool DryRun { get; set; } = true;
}

public sealed record CorreccionCompletadoAccionDto(
    int MovimientoId,
    string NumeroMovimiento,
    int LoteAveEngordeId,
    int AntesH,
    int AntesM,
    int AntesX,
    int DespuesH,
    int DespuesM,
    int DespuesX,
    int DevueltoAlLoteH,
    int DevueltoAlLoteM,
    int DevueltoAlLoteX,
    string Nota
);

public sealed class CorregirVentasCompletadasResponse
{
    public bool Ok { get; set; }
    public bool DryRun { get; set; }
    public string? Mensaje { get; set; }
    public List<CorreccionCompletadoAccionDto> Acciones { get; set; } = new();
}

