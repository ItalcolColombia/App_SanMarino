// src/ZooSanMarino.Application/DTOs/LoteSeguimientoDto.cs
using System.Text.Json;

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
    decimal? ConsumoKgMachos,
    string?  Observaciones,
    string?  Ciclo,
    // Campos de peso y uniformidad
    double?  PesoPromH,
    double?  PesoPromM,
    double?  UniformidadH,
    double?  UniformidadM,
    double?  CvH,
    double?  CvM,
    // Campos de agua
    double?  ConsumoAguaDiario,
    double?  ConsumoAguaPh,
    double?  ConsumoAguaOrp,
    double?  ConsumoAguaTemperatura,
    // Metadata e items adicionales
    JsonDocument? Metadata,
    JsonDocument? ItemsAdicionales
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
    decimal? ConsumoKgMachos,
    string?  Observaciones,
    string?  Ciclo,
    // Campos de peso y uniformidad
    double?  PesoPromH,
    double?  PesoPromM,
    double?  UniformidadH,
    double?  UniformidadM,
    double?  CvH,
    double?  CvM,
    // Campos de agua
    double?  ConsumoAguaDiario,
    double?  ConsumoAguaPh,
    double?  ConsumoAguaOrp,
    double?  ConsumoAguaTemperatura,
    // Metadata e items adicionales
    JsonDocument? Metadata,
    JsonDocument? ItemsAdicionales
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
    decimal? ConsumoKgMachos,
    string?  Observaciones,
    string?  Ciclo,
    // Campos de peso y uniformidad
    double?  PesoPromH,
    double?  PesoPromM,
    double?  UniformidadH,
    double?  UniformidadM,
    double?  CvH,
    double?  CvM,
    // Campos de agua
    double?  ConsumoAguaDiario,
    double?  ConsumoAguaPh,
    double?  ConsumoAguaOrp,
    double?  ConsumoAguaTemperatura,
    // Metadata e items adicionales
    JsonDocument? Metadata,
    JsonDocument? ItemsAdicionales
);
