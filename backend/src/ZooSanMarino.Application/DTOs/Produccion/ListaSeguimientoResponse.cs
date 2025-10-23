// src/ZooSanMarino.Application/DTOs/Produccion/ListaSeguimientoResponse.cs
namespace ZooSanMarino.Application.DTOs.Produccion;

public record ListaSeguimientoResponse(
    IList<SeguimientoItemDto> Items,
    int Total
);



