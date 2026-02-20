// file: src/ZooSanMarino.Application/DTOs/LoteReproductoraDto.cs
namespace ZooSanMarino.Application.DTOs;

public record LoteReproductoraDto(
    string    LoteId,  // Cambiado a string para coincidir con character varying(64) en BD
    string    ReproductoraId,
    string    NombreLote,
    DateTime? FechaEncasetamiento,
    int?      M,
    int?      H,
    int?      Mixtas,
    int?      MortCajaH,
    int?      MortCajaM,
    int?      UnifH,
    int?      UnifM,
    decimal?  PesoInicialM,
    decimal?  PesoInicialH,
    decimal?  PesoMixto
);

public record CreateLoteReproductoraDto(
    string    LoteId,  // Cambiado a string para coincidir con character varying(64) en BD
    string    ReproductoraId,
    string    NombreLote,
    DateTime? FechaEncasetamiento,
    int?      M,
    int?      H,
    int?      Mixtas,
    int?      MortCajaH,
    int?      MortCajaM,
    int?      UnifH,
    int?      UnifM,
    decimal?  PesoInicialM,
    decimal?  PesoInicialH,
    decimal?  PesoMixto
);

public record UpdateLoteReproductoraDto(
    string    LoteId,  // Cambiado a string para coincidir con character varying(64) en BD
    string    ReproductoraId,
    string    NombreLote,
    DateTime? FechaEncasetamiento,
    int?      M,
    int?      H,
    int?      Mixtas,
    int?      MortCajaH,
    int?      MortCajaM,
    int?      UnifH,
    int?      UnifM,
    decimal?  PesoInicialM,
    decimal?  PesoInicialH,
    decimal?  PesoMixto
);
