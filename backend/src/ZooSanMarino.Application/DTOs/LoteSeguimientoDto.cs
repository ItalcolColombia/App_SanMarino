// src/ZooSanMarino.Application/DTOs/LoteSeguimientoDto.cs
namespace ZooSanMarino.Application.DTOs;

public record LoteSeguimientoDto(
    int      Id,
    DateTime Fecha,
    int      LoteId,
    string   ReproductoraId,
    decimal? PesoInicial,
    decimal? PesoFinal,
    int?     MortalidadM,
    int?     MortalidadH,
    int?     SelM,
    int?     SelH,
    int?     ErrorM,
    int?     ErrorH,
    string?  TipoAlimento,
    decimal? ConsumoAlimento,
    string?  Observaciones
);

public record CreateLoteSeguimientoDto(
    DateTime Fecha,
    int      LoteId,
    string   ReproductoraId,
    decimal? PesoInicial,
    decimal? PesoFinal,
    int?     MortalidadM,
    int?     MortalidadH,
    int?     SelM,
    int?     SelH,
    int?     ErrorM,
    int?     ErrorH,
    string?  TipoAlimento,
    decimal? ConsumoAlimento,
    string?  Observaciones
);

public record UpdateLoteSeguimientoDto(
    int      Id,
    DateTime Fecha,
    int      LoteId,
    string   ReproductoraId,
    decimal? PesoInicial,
    decimal? PesoFinal,
    int?     MortalidadM,
    int?     MortalidadH,
    int?     SelM,
    int?     SelH,
    int?     ErrorM,
    int?     ErrorH,
    string?  TipoAlimento,
    decimal? ConsumoAlimento,
    string?  Observaciones
);
