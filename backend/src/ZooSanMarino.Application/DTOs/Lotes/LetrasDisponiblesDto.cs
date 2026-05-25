namespace ZooSanMarino.Application.DTOs.Lotes;

/// <summary>
/// Letras A-F disponibles y ocupadas para un prefijo de lote en un galpón específico.
/// </summary>
public record LetrasDisponiblesDto(
    List<string> LetrasOcupadas,
    List<string> LetrasDisponibles
);
