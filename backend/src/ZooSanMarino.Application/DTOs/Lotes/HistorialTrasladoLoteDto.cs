// src/ZooSanMarino.Application/DTOs/Lotes/HistorialTrasladoLoteDto.cs
namespace ZooSanMarino.Application.DTOs.Lotes;

public sealed record HistorialTrasladoLoteDto(
    int Id,
    int LoteOriginalId,
    int LoteNuevoId,
    int GranjaOrigenId,
    string GranjaOrigenNombre,
    int GranjaDestinoId,
    string GranjaDestinoNombre,
    string? NucleoDestinoId,
    string? NucleoDestinoNombre,
    string? GalponDestinoId,
    string? GalponDestinoNombre,
    string? Observaciones,
    int CreatedByUserId,
    string CreatedByUserName,
    DateTime CreatedAt
);

