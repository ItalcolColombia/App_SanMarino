// src/ZooSanMarino.Application/DTOs/LoteGalponDto.cs
namespace ZooSanMarino.Application.DTOs;
public record LoteGalponDto(
    int      LoteId,  // Changed from string to int
    string   ReproductoraId,
    string   GalponId,
    int?     M,
    int?     H
);

public record CreateLoteGalponDto(
    int      LoteId,  // Changed from string to int
    string   ReproductoraId,
    string   GalponId,
    int?     M,
    int?     H
);

public record UpdateLoteGalponDto(
    int      LoteId,  // Changed from string to int
    string   ReproductoraId,
    string   GalponId,
    int?     M,
    int?     H
);
