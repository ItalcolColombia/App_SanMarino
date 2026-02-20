// src/ZooSanMarino.Application/DTOs/LoteGalponDto.cs
namespace ZooSanMarino.Application.DTOs;
public record LoteGalponDto(
    string   LoteId,  // Cambiado a string para coincidir con character varying(64) en BD
    string   ReproductoraId,
    string   GalponId,
    int?     M,
    int?     H
);

public record CreateLoteGalponDto(
    string   LoteId,  // Cambiado a string para coincidir con character varying(64) en BD
    string   ReproductoraId,
    string   GalponId,
    int?     M,
    int?     H
);

public record UpdateLoteGalponDto(
    string   LoteId,  // Cambiado a string para coincidir con character varying(64) en BD
    string   ReproductoraId,
    string   GalponId,
    int?     M,
    int?     H
);
