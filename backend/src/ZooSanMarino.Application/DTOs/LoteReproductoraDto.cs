// file: src/ZooSanMarino.Application/DTOs/LoteReproductoraDto.cs
namespace ZooSanMarino.Application.DTOs;

public record LoteReproductoraDto(
    int       LoteId,
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
    int       LoteId,
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
    int       LoteId,
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
