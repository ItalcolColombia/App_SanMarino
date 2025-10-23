// src/ZooSanMarino.Application/DTOs/Produccion/SeguimientoItemDto.cs
namespace ZooSanMarino.Application.DTOs.Produccion;

public record SeguimientoItemDto(
    int Id,
    int ProduccionLoteId,
    DateTime FechaRegistro,
    int MortalidadH,
    int MortalidadM,
    decimal ConsumoKg,
    int HuevosTotales,
    int HuevosIncubables,
    decimal PesoHuevo,
    string? Observaciones,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);



